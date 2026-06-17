using System;

namespace Falcom
{
   public interface IFalcomEvent
   {
      Guid EventId { get; }
      DateTime Timestamp { get; }
      string Source { get; }
      bool IsStateTrigger { get; }
   }

   public abstract class FalcomEventBase : IFalcomEvent
   {
      public Guid EventId { get; } = Guid.NewGuid();
      public DateTime Timestamp { get; } = DateTime.UtcNow;
      public abstract string Source { get; }
      public abstract bool IsStateTrigger { get; }
   }
}