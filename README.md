# 🌱Easy Dead-Letter 🌱

## 📕Handling dead-letter queues in EasyNetQ (RabbitMQ) 
  
Messages from a queue can be "dead-lettered"; that is, republished to an exchange when any of the following events occur:

> - The message is negatively acknowledged by a consumer using basic.reject or basic.nack with requeue parameter set to false.
> - The message expires due to per-message TTL;
> - The message is dropped because its queue exceeded a length limit
 
## 🔔 Problem
If you are using RabbitMQ client for dotnet there is no problem, you can define dead-letter queues during a queue declaration, But using RabbitMQ client brings lots of implementation complexity to the application, because of that so many developers prefer to use the **[EasyNetQ](https://github.com/EasyNetQ/EasyNetQ)** library which is a wrapper on RabbitMQ client(**Amazing and simple**).
There is no implementation for **dead-letter queues** in EasyNetQ to keep the library easy to use, It means in case of any exception in the message consumers, the message will be moved to the default error queue of RabbitMQ, and for an enterprise application, it's not good idea to keep all the failure messages in one queue, because maybe different handling mechanism needs to be applied to those failed messages according to the type of the message.

## 🔔 Solution
In one of my projects there was a requirement to handle failed messages **discretely** and **moving each failed message to the related dead-letter queue**.
For example, if there is a queue named **Product.Report**, in case of any exception the message should be moved to a new queue named **Product.Report.deadletter**. Of course this deadletter queue is a typical queue with the same message type, in order to consume them with another approach.
This functionality can be achieved via the **EasyDeadLetter** NuGet package. you can find it on **[Nuget](https://www.nuget.org/packages/EasyDeadLetterStrategy/) website.**

## 🔔How it works
- First of all, Decorate your class object with **QeueuAttribute**
```
 [Queue("Product.Report", ExchangeName = "Product.Report")]
 public class ProductReport { }
```

- The second step is to define your dead-letter queue with the same **QueueAttribute** and also **inherit** the dead-letter object from the Main object class.
 ```
 [Queue("Product.Report.DeadLetter", ExchangeName = "Product.Report.DeadLetter")]
 public class ProductReportDeadLetter : ProductReport { }
```

- Now, it's time to decorate your main queue object with the **EasyDeadLetter** attribute and set the type of dead-letter queue.
```
[EasyDeadLetter(DeadLetterType = typeof(ProductReportDeadLetter))]
[Queue("Product.Report", ExchangeName = "Product.Report")]
public class ProductReport { }
```
- In the final step, you need to register **EasyDeadLetterStrategy**  as the default error handler **(IConsumerErrorStrategy)** in the **startup.cs**
```
services.AddSingleton<IBus>(RabbitHutch.CreateBus("connectionString",
                                    serviceRegister =>
                                    {
                                        serviceRegister.Register<IConsumerErrorStrategy, EasyDeadLetterStrategy>();                                        
                                    }));
```


That's all. from now on any failed message will be moved to the related dead-letter queue


> Note: Take it into consideration :
- The main queue object is decorated by the **EasyDeadLetter** attribute
- Register **EasyDeadLetterStrategy** as the default ErrorHandler 
- any object which is not decorated with the **EasyDeadLetter** will be handled by the default mechanism of the EasyNetQ and RabbitMQ (will be saved into the **default error queue**).
- In case of any exception in this NuGet package, the message will be moved to the  **default error queue**. and there is an **Exception** property for each message in this queue, which can be checked, and find the root cause of the issue.

## Final though
Feel free to send me your opinion. 👋 🔔🌱

You can download the Nuget package from [Here](https://www.nuget.org/packages/EasyDeadLetterStrategy/)

