using CRM.Client.Models;
using CRM.Client.Services;
using CRM.Server.Services;
using CRM.Shared;
using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Azure;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TicketFeedbacksController : ControllerBase
    {
        private readonly ITicketFeedbackService _feedbackService;

        private readonly ILogEventService _logEventService;

        private readonly IPermitsService _permitsService;
        public TicketFeedbacksController(ITicketFeedbackService feedbackService, ILogEventService logEventService, IPermitsService permitsService)
        {
            _feedbackService = feedbackService;
            _logEventService = logEventService;
            _permitsService = permitsService;
        }

        [HttpGet]
        public async Task<PagingResponse<TicketFeedbackResponse>?> GetPage([FromQuery] TicketFeedbackFilterModel? args = null)
        {
            try
            {
                var items = await _feedbackService.GetPagingAsync(args);

                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketFeedbacksController), nameof(GetPage), LogEvent.EventsTypes.Error, ex);
                return null;
            }
        }

        [HttpGet("list")]
        public async Task<IEnumerable<TicketFeedbackResponse>?> GetItems([FromQuery] TicketFeedbackFilterModel? args = null)
        {
            try
            {
                var items = await _feedbackService.GetListAsync(args);
                if (items == null)
                {
                    return Enumerable.Empty<TicketFeedbackResponse>();
                }
                return items;
            }
            catch (Exception ex)
            {
                await _logEventService.RegisterAsync(nameof(TicketFeedbacksController), nameof(GetItems), LogEvent.EventsTypes.Error, ex);
                return Enumerable.Empty<TicketFeedbackResponse>();
            }
        }


        //[HttpGet("{id}")]
        //public async Task<ActionResult<TicketFeedbackResponse>> Get(int id)
        //{
        //    var item = await _feedbackService.GetItemAsync(id);

        //    if (item == null)
        //    {
        //        return NotFound();
        //    }
        //    return item;
        //}

        // PUT: api/Companies/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754

        [HttpPut("{id}")]
        public async Task<ActionResult<APIResponseMessage<TicketFeedback>>> Put(int id, TicketFeedback item)
        {
            if (id != item.Id)
            {
                return BadRequest();
            }
            var resp = await _feedbackService.PostAsync(item);

            if (resp == null)
                return Problem("Error saving settings");

            return Ok(resp);
        }

        //// POST: api/Companies
        //// To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        //[HttpPost]
        //public async Task<ActionResult<APIResponseMessage<TicketFeedback>>> Post(TicketFeedbackRequest item)
        //{
        //    var resp = await _feedbackService.PostAsync(item);

        //    if (resp == null)
        //        return StatusCode(StatusCodes.Status500InternalServerError, "Post return null");

        //    return Ok(resp);
        //}

        /// <summary>
        /// Ottiene i ticket chiusi in attesa di feedback per l'utente corrente
        /// </summary>
        [HttpGet("pending")]
        public async Task<ActionResult<List<TicketPendingFeedback>>> GetPendingFeedbacks()
        {
            try
            {
                var result = await _feedbackService.GetPendingFeedbacksAsync();
                return Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Errore interno: {ex.Message}");
            }
        }

        /// <summary>
        /// Ottiene il conteggio dei ticket in attesa di feedback
        /// </summary>
        [HttpGet("pending/count")]
        public async Task<ActionResult<int>> GetPendingFeedbacksCount()
        {
            try
            {
                var count = await _feedbackService.GetPendingFeedbacksCountAsync();
                return Ok(count);
            }
            catch (UnauthorizedAccessException)
            {
                return Unauthorized();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Errore interno: {ex.Message}");
            }
        }

        /// <summary>
        /// Crea un nuovo feedback per un ticket
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<TicketFeedbackResponse>> CreateFeedback([FromBody] TicketFeedbackRequest request)
        {
            try
            {
                var response = await _feedbackService.CreateFeedbackAsync(request);
                return CreatedAtAction(nameof(GetFeedback), new { id = response.Id }, response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Errore interno: {ex.Message}");
            }
        }

        /// <summary>
        /// Ottiene un feedback specifico
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<TicketFeedbackResponse>> GetFeedback(int id)
        {
            try
            {
                var feedback = await _feedbackService.GetFeedbackAsync(id);
                if (feedback == null)
                    return NotFound($"Feedback #{id} non trovato");

                return Ok(feedback);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Errore interno: {ex.Message}");
            }
        }

        /// <summary>
        /// Ottiene il feedback di un ticket specifico
        /// </summary>
        [HttpGet("ticket/{ticketId}")]
        public async Task<ActionResult<TicketFeedbackResponse>> GetFeedbackByTicket(int ticketId)
        {
            try
            {
                var feedback = await _feedbackService.GetFeedbackByTicketAsync(ticketId);
                if (feedback == null)
                    return NotFound($"Nessun feedback per il ticket #{ticketId}");

                return Ok(feedback);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Errore interno: {ex.Message}");
            }
        }

        ///// <summary>
        ///// Ottiene tutti i feedback (solo per admin)
        ///// </summary>
        //[HttpGet]
        //[Authorize(Policy = "AdminRole")]
        //public async Task<ActionResult<List<TicketFeedbackResponse>>> GetAllFeedbacks([FromQuery] bool unreadOnly = false)
        //{
        //    try
        //    {
        //        var feedbacks = await _feedbackService.GetAllFeedbacksAsync(unreadOnly);
        //        return Ok(feedbacks);
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, $"Errore interno: {ex.Message}");
        //    }
        //}

        /// <summary>
        /// Segna un feedback come letto (solo per admin)
        /// </summary>
        [HttpPut("{id}/read")]
        [Authorize(Policy = "AdminRole")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            try
            {
                var success = await _feedbackService.MarkAsReadAsync(id);
                if (!success)
                    return NotFound($"Feedback #{id} non trovato");

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Errore interno: {ex.Message}");
            }
        }

        /// <summary>
        /// Salta il feedback per un ticket
        /// </summary>
        [HttpPost("skip/{ticketId}")]
        public async Task<IActionResult> SkipFeedback(int ticketId)
        {
            try
            {
                await _feedbackService.SkipFeedbackAsync(ticketId);
                return Ok(new { message = "Feedback saltato" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Errore interno: {ex.Message}");
            }
        }

        [HttpGet("average")]
        public async Task<ActionResult<AverageFeedbackDTO>> AverageRate()
        {
            try
            {
                var averageFeedback = await _feedbackService.AverageRateAsync();



                return Ok(averageFeedback);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Errore interno: {ex.Message}");
            }
        }
    }
}
