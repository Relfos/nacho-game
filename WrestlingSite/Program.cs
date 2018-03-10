using SynkServer.Core;
using SynkServer.HTTP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WrestlingSite
{
    class Program
    {
        static void Main(string[] args)
        {
            // initialize a logger
            var log = new SynkServer.Core.Logger();

            // either parse the settings from the program args or initialize them manually
            var settings = ServerSettings.Parse(args);

            var server = new HTTPServer(log, settings);

            // instantiate a new site, the second argument is the file path where the public site contents will be found
            var site = new Site(server, "public");

            site.Get("/", (request) =>
            {
                return HTTPResponse.Redirect("/index.html");
            });

            server.Run();
        }
    }
}
