using CRM.Shared.DTOs;
using CRM.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CRM.Server.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TicketFeedbackController : ControllerBase
    {
        private readonly ITicketFeedbackService _feedbackService;

        public TicketFeedbackController(ITicketFeedbackService feedbackService)
        {
            _feedbackService = feedbackService;
        }

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

        /// <summary>
        /// Ottiene tutti i feedback (solo per admin)
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "AdminRole")]
        public async Task<ActionResult<List<TicketFeedbackResponse>>> GetAllFeedbacks([FromQuery] bool unreadOnly = false)
        {
            try
            {
                var feedbacks = await _feedbackService.GetAllFeedbacksAsync(unreadOnly);
                return Ok(feedbacks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Errore interno: {ex.Message}");
            }
        }

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
    }
}
