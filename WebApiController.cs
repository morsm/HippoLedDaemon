using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;


namespace Termors.Serivces.HippotronicsLedDaemon
{
    public class WebApiController : ApiController
    {
        [Route("webapi/html"), HttpGet]
        public HttpResponseMessage GetWebPage()
        {
            var result = WebPageGenerator.GenerateWebPage(GetLamps());

            var res = Request.CreateResponse(HttpStatusCode.OK);
            res.Content = new StringContent(result.ToString(), System.Text.Encoding.UTF8, "text/html");

            return res;
        }

        [Route("webapi/refresh"), HttpGet]
        public void Refresh()
        {
            // No real function anymore
        }

        [Route("webapi/lamps"), HttpGet]
        public LampNode[] GetLamps()
        {
            RuntimeMonitor.Monitor.RegisterCall("LampsGetAll");

            using (var db = new DatabaseClient())
            {
                var records = db.GetAll();
                List<LampNode> retval = new List<LampNode>();

                foreach (var r in records) retval.Add(r);
                return retval.ToArray();
            }
        }

        [Route("webapi/lamp/{id}"), HttpGet]
        public LampNode GetLamp(string id)
        {
            RuntimeMonitor.Monitor.RegisterCall("LampGet");

            using (var db = new DatabaseClient())
            {
                var record = db.GetOne(id);
                return record;
            }
        }

        [Route("webapi/lamp/{id}"), HttpPost]
        public LampNode SetLampState(string id, SetLampData data)
        {
            return SetLampStateExtended(id, new SetLampDataExtended(data));
        }

        [Route("webapi/lampstate/{id}"), HttpPost]
        public LampNode SetLampStateExtended(string id, SetLampDataExtended data)
        {
            LampNode record = null;

            RuntimeMonitor.Monitor.RegisterCall("LampSet");

            record = GetLamp(id);
            record.ProcessStateChanges(data);

            RequestSequencer.Sequencer.Schedule(new LampRequest(id, data));

            return record;
        }
    }
}
