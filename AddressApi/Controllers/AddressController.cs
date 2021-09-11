using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace AddressApi.Controllers
{
    public class AddressController : ApiController
    {
        private static string _address = "";

        // GET api/Address
        public string Get()
        {
            return _address;
        }

        // POST api/Address
        public void Post([FromUri] string address)
        {
            _address = address;
        }
    }
}
