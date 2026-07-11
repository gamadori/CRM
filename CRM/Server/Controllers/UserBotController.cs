using CRM.Server.Services;
using CRM.Shared;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using TL;

namespace CRM.Server.Controllers
{
	
	[ApiController]
	[Route("api/[controller]")]
	public class UserBotController : ControllerBase
	{
		private readonly WTelegramService WT;
		public UserBotController(WTelegramService wt) => WT = wt;


		[HttpGet("status")]
		public ActionResult<TelegramStatus> Status()
		{
			switch (WT.ConfigNeeded)
			{
				case "connecting":
					return new TelegramStatus() { State = TelegramButState.Connecting };
				case "unavailable":
					return new TelegramStatus() { State = TelegramButState.ConfigNeed, Desc = WT.LastError ?? "Telegram non disponibile" };
				case null: 
					return new TelegramStatus() { State = TelegramButState.Connected, User = WT.User?.phone  };
				default: 
					return new TelegramStatus() { State = TelegramButState.ConfigNeed, Desc = WT.ConfigNeeded };
			}
		}

		[HttpGet("config")]
		public async Task<ActionResult> Config([FromQuery] string value)
		{
			await WT.DoLogin(value);
			return Redirect("status");
		}

		[HttpGet("chats")]
		public async Task<object> Chats()
		{
			if (WT.User == null) return StatusCode(503, WT.LastError ?? "Complete the login first");
			if (!WT.TryGetClient(out var client)) return StatusCode(503, WT.LastError ?? "Telegram non disponibile");
			var chats = await client.Messages_GetAllChats();
		
			return chats.chats;
		}
	}
}
