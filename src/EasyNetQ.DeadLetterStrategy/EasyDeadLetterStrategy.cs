using System;
using System.Linq;
using RabbitMQ.Client;
using System.Reflection;
using EasyNetQ.Consumer;
using System.Collections.Generic;

namespace EasyNetQ.EasyDeadLetter
{
    /// <summary>
    /// DeadLetter does not implemented by EasyNetQ library.
    /// The handling mechanism of the RabbitMQ failed messages is implemented in the HandleConsumerError class
    /// Here this functionality is overrided.
    /// From now on, all the failed messages instead of being moved to the default error queue, 
    /// will be moved to a newly and automatically created dead letter exchange and dead letter queue.
    /// Each Queue can possess its own specfic eadLetter queue, to achive this, the Queue object should be
    /// decorated with the EasyDeadLetter attribute.
    /// </summary>
    public class EasyDeadLetterStrategy : DefaultConsumerErrorStrategy
    {
        private const bool _durableQueue = true;
        private const bool _autoDelete = false;
        private const string _routingKey = "#";        
        private readonly IPersistentConnection persistentConnection;       

        private readonly Dictionary<string, string> DefinedDeadLetters =
                                                            new Dictionary<string, string>();

        private readonly Dictionary<string, EasyDeadLetterAttribute> 
            QueuesWithDeadLetterAttribute = new Dictionary<string, EasyDeadLetterAttribute>();


        public EasyDeadLetterStrategy(
               IPersistentConnection persistentConnection,
               ISerializer serializer,
               IConventions conventions,
               ITypeNameSerializer typeNameSerializer,
               IErrorMessageSerializer errorMessageSerializer,
               ConnectionConfiguration configuration)
            : base(persistentConnection,
                    serializer,
                    conventions,
                    typeNameSerializer,
                    errorMessageSerializer,
                    configuration)
        {
            this.persistentConnection = persistentConnection;

            LoadTypesWithDeadLetterAttribute();
        }

      
        /// <summary>
        /// overriding the error handling mechanism by the new approach
        /// </summary>
        /// <param name="context">includes message boody,ReceivedInformation</param>
        /// <param name="exception">The exception detail of the failure message consumer</param>
        /// <returns></returns>
        public override AckStrategy HandleConsumerError
            (ConsumerExecutionContext context, Exception exception)
        {
            try
            {
                //one more time requeued for sake of transient exception
                if (!context.ReceivedInfo.Redelivered)
                    return AckStrategies.NackWithRequeue;


                if (context.Properties.Type == null)
                    return base.HandleConsumerError(context, exception);

                QueuesWithDeadLetterAttribute.TryGetValue(context.Properties.Type, out var deadLetterInfo);

                //It means, there is no dead letter implmenetation for this message
                if (deadLetterInfo == null)
                    return base.HandleConsumerError(context, exception);


                using var model = persistentConnection.CreateModel();

                var deadLetterExchange = DeclareDeadLetterExchangeAndQueue(model, deadLetterInfo);

                model.BasicPublish(
                    deadLetterExchange,
                    context.ReceivedInfo.RoutingKey,
                    GetProperties(model, deadLetterInfo.DeadLetterType.Name),
                    context.Body);


                return AckStrategies.Ack;
            }
            catch (Exception ex)
            {
                var refinedException = new AggregateException(
                    $"EasyDeadLetter exception:{ex?.Message}." +
                    $"Original Exception :{exception.Message}" ,
                    ex?.InnerException, exception?.InnerException);

                return base.HandleConsumerError(context, refinedException);
            }
        }

        /// <summary>
        /// Loading all the types which are decorated with EasyDeadLetter attribute,     
        /// </summary>
        /// <exception cref="Exception">In case of failure to collect types information, 
        /// an exception with all detail will be thrown</exception>
        private void LoadTypesWithDeadLetterAttribute()
        {
            try
            {
                foreach (Assembly assm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (Type type in assm.GetTypes())
                    {

                        if (type.IsClass && type.IsDefined(typeof(EasyDeadLetterAttribute)))
                        {
                            QueuesWithDeadLetterAttribute.Add
                                (type.Name, (EasyDeadLetterAttribute)type.GetCustomAttribute
                                (typeof(EasyDeadLetterAttribute)));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                QueuesWithDeadLetterAttribute.Clear();

                throw new Exception($"An Exception occurred in " +
                    $"EasyDeadLetterStrategy.LoadTypesWithDeadLetterAttribute." +
                    $" Message:{ex?.Message}. InnerException:{ex?.InnerException}");
            }
        }

        /// <summary>
        /// Create a new properties object for the new deadLetter message
        /// </summary>
        /// <param name="model"></param>
        /// <param name="typeName"></param>
        /// <returns></returns>
        private IBasicProperties GetProperties(IModel model, string typeName)
        {
            var properties = model.CreateBasicProperties();
            
            properties.Type = typeName;
            properties.Persistent = true;

            return properties;
        }

        /// <summary>
        /// Create a new DeadLetter Exhcange and queue ,in case it does not exist
        /// </summary>
        /// <param name="model">RabbitMQ model instance</param>
        /// <param name="deadLetterInfo">deadLetter attribute detail</param>
        /// <returns></returns>
        /// <exception cref="Exception">In case of null value for te DeadLetter attribute , the exception will be thrown
        /// and the message will be processed by the default mechanism of EasyNetQ</exception>
        private string DeclareDeadLetterExchangeAndQueue
            (IModel model, EasyDeadLetterAttribute deadLetterInfo)
        {

            var queueAttribute = deadLetterInfo.DeadLetterType.CustomAttributes
                    .Where(s => s.AttributeType == typeof(QueueAttribute))
                    .FirstOrDefault();

            if (queueAttribute == null)
                throw new Exception(
                    $"The QueueAttribute should be set on a dead Letter object." +
                    $"name : {deadLetterInfo.DeadLetterType.Name}.");


            var queueAttributes = deadLetterInfo.DeadLetterType.GetCustomAttribute<QueueAttribute>();
            var refinedQueueName = GenerateQueueNameByEasyNetQConvention
                (queueAttributes.QueueName,deadLetterInfo.DeadLetterType.Name);
            var refinedExchangeName = queueAttributes.ExchangeName;


            if (!DefinedDeadLetters.ContainsKey(refinedQueueName))
            {
                
                model.ExchangeDeclare(refinedExchangeName, "topic", _durableQueue,_autoDelete, null);

                model.QueueDeclare(refinedQueueName, _durableQueue, false, _autoDelete, null);

                model.QueueBind(refinedQueueName, refinedExchangeName, _routingKey);

                DefinedDeadLetters.Add(refinedQueueName, refinedExchangeName);
            }

            return DefinedDeadLetters[refinedQueueName];
        }

        
        /// <summary>
        /// Create a dead letter queue name regarding to the EasyNetQ queue naming convention.
        /// </summary>
        /// <param name="queueName"></param>
        /// <param name="typeName"></param>
        /// <returns></returns>
        private string GenerateQueueNameByEasyNetQConvention(string queueName, string typeName)
            => $"{queueName}_{typeName}";
    }
}





