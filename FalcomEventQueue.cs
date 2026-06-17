using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Falcom
{
   public sealed class FalcomEventQueue
   {
      // Wir nutzen einen Unbounded Channel, damit die OPC-Clients 
      // ihre Events blitzschnell und ohne Blockade abladen können.
      private readonly Channel<IFalcomEvent> _channel = Channel.CreateUnbounded<IFalcomEvent>(
          new UnboundedChannelOptions
          {
             SingleReader = true,  // Wichtig: Nur der Dispatcher-Worker liest!
             SingleWriter = false  // Mehrere OPC-Clients + DB-Poller schreiben parallel.
          });

      public ChannelWriter<IFalcomEvent> Writer => _channel.Writer;
      public ChannelReader<IFalcomEvent> Reader => _channel.Reader;

      public async ValueTask PushEventAsync(IFalcomEvent falcomEvent)
      {
         await _channel.Writer.WriteAsync(falcomEvent);
      }
   }
}