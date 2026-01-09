using System;
using WebPush;

// ? Script C# diretto - Esegui con: dotnet script GenerateVapidKeysScript.csx

var vapidKeys = VapidHelper.GenerateVapidKeys();

Console.WriteLine("======================================");
Console.WriteLine("  ?? VAPID KEYS GENERATE SUCCESS!");
Console.WriteLine("======================================");
Console.WriteLine();
Console.WriteLine("?? Copia queste chiavi in CRM/Server/appsettings.PushNotifications.json:");
Console.WriteLine();
Console.WriteLine(@"
{
  ""PushNotifications"": {
    ""WebPush"": {
      ""subject"": ""mailto:info@a-plusautomation.com"",
      ""publicKey"": """ + vapidKeys.PublicKey + @""",
      ""privateKey"": """ + vapidKeys.PrivateKey + @""
    }
  }
}
");
Console.WriteLine();
Console.WriteLine("??  ATTENZIONE: La chiave PRIVATA va tenuta SEGRETA!");
Console.WriteLine("     NON committare su Git se il repository è pubblico.");
Console.WriteLine();
Console.WriteLine("? Dopo aver copiato le chiavi, riavvia il server CRM.");
Console.WriteLine();
