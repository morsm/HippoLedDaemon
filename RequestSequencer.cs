using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace Termors.Serivces.HippotronicsLedDaemon
{
    public class LampRequest
    {
        public LampRequest(String id, SetLampDataExtended data)
        {
            Id = id;
            Data = data;
        }

        public String Id { get; }
        public SetLampDataExtended Data { get; }

        public override string ToString()
        {
            return String.Format("Id: [{0}], SetLampData: [{1}]", Id, Data);
        }
    }

    public class RequestSequencer
    {
        protected Queue<LampRequest> _queue = new Queue<LampRequest>();
        protected Mutex _mutex = new Mutex(false);

        public static RequestSequencer Sequencer = new RequestSequencer();

        protected RequestSequencer()
        {
        }

        public void Schedule(LampRequest request)
        {
            lock (_queue) _queue.Enqueue(request);

            // Is there already a task running to service the request?
            bool noThread = _mutex.WaitOne(0);
            if (noThread)
            {
                Task.Run(() => ServiceRequests());
                _mutex.ReleaseMutex();
            }
        }

        protected void ServiceRequests()
        {
            // Obtain Mutex to indicate that we are servicing requests
            _mutex.WaitOne();

            LampRequest request = null;
            try
            {
                while (true)
                {
                    // Seems like infinite loop, but empty queue will throw exception

                    lock (_queue) request = _queue.Dequeue();

                    Serve(request);

                    // If there are more requests, sneak in a little delay to make sure the lamps can keep up
                    _queue.Peek();
                    // If code made it here without exception, there is another item
                    Thread.Sleep(200);
                }

            }
            catch (InvalidOperationException)
            {
                // Queue is empty, this is fine. This thread will end.
            }
            catch (Exception ex)
            {
                // Error processing request
                Logger.LogError("Error processing request {0}, Exception {1} Message {2}", request, ex.GetType().Name, ex.Message);
            }
            finally
            {
                // Release Mutex to let new thread be started
                _mutex.ReleaseMutex();
            }
        }

        protected virtual void Serve(LampRequest request)
        {
            var record = GetLampDb(request.Id);
            var client = new LampClient(record);

            record.ProcessStateChanges(request.Data);

            client.SetState().Wait();       // Synchronously set the state. It may throw an exception

            UpdateLampDb(record);
            
        }

        protected LampNode GetLampDb(String id)
        {
            using (var db = new DatabaseClient())
            {
                var record = db.GetOne(id);
                return record;
            }
        }

        protected void UpdateLampDb(LampNode record)
        {
            using (var db = new DatabaseClient())
            {
                db.AddOrUpdate(record);             // Update the record to the database
            }
        }

    }
}
