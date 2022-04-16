using System;

namespace EasyNetQ.EasyDeadLetter
{
    /// <summary>
    /// Decorate any queue object if you need a deadletter to be created for it
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class EasyDeadLetterAttribute : Attribute 
    {
        /// <summary>
        /// determine the type of the dead letter object for this queue
        /// </summary>
        public Type DeadLetterType { get; set; }
    }
}
