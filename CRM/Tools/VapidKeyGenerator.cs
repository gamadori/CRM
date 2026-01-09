using System;
using WebPush;

namespace VapidKeyGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            var vapidKeys = VapidHelper.GenerateVapidKeys();
            
            Console.WriteLine("==== VAPID KEYS GENERATE ====");
            Console.WriteLine();
            Console.WriteLine("Public Key:");
            Console.WriteLine(vapidKeys.PublicKey);
            Console.WriteLine();
            Console.WriteLine("Private Key:");
            Console.WriteLine(vapidKeys.PrivateKey);
            Console.WriteLine();
            Console.WriteLine("==== COPIA IN appsettings.json ====");
            Console.WriteLine(@"
""WebPush"": {
  ""publicKey"": """ + vapidKeys.PublicKey + @""",
  ""privateKey"": """ + vapidKeys.PrivateKey + @""",
  ""subject"": ""mailto:support@yourcrm.com""
}
");
        }
    }
}
