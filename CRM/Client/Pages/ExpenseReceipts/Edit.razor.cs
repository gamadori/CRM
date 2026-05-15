using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.ExpenseReceipts
{
    [Authorize]
    public partial class Edit : ComponentBase
    {
        [Parameter] public int InterventionId { get; set; }
        [Parameter] public int ReceiptId { get; set; }

        private ExpenseReceiptCreateUpdateDTO _model = new();
        private bool _isLoading = true;
        private bool _isSaving = false;
        private bool _notFound = false;
        private string _errorMessage;

        protected override async Task OnInitializedAsync()
        {
            await LoadReceipt();
        }

        private async Task LoadReceipt()
        {
            try
            {
                _isLoading = true;
                var receipt = await Http.GetFromJsonAsync<ExpenseReceiptDTO>($"api/ExpenseReceipts/{ReceiptId}");

                if (receipt == null)
                {
                    _notFound = true;
                    return;
                }

                _model = new ExpenseReceiptCreateUpdateDTO
                {
                    Id = receipt.Id,
                    TicketInterventionId = receipt.TicketInterventionId,
                    Description = receipt.Description,
                    AttachmentFileId = receipt.AttachmentFileId,
                    TotalAmount = receipt.TotalAmount,
                    TaxAmount = receipt.TaxAmount,
                    TransactionDate = receipt.TransactionDate,
                    MerchantName = receipt.MerchantName,
                    Currency = receipt.Currency,
                    Notes = receipt.Notes,
                    IsConfirmed = receipt.IsConfirmed,
                    ExtractionConfidence = receipt.ExtractionConfidence
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore caricamento nota spese: {ex.Message}");
                _notFound = true;
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task SaveReceipt()
        {
            try
            {
                _isSaving = true;
                _errorMessage = null;

                var response = await Http.PutAsJsonAsync($"api/ExpenseReceipts/{ReceiptId}/Details", _model);

                if (response.IsSuccessStatusCode)
                {
                    NavigationManager.NavigateTo($"/TicketInterventions/{InterventionId}/ExpenseReceipts/{ReceiptId}/Details");
                }
                else
                {
                    _errorMessage = "Errore durante il salvataggio della nota spese.";
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Errore: {ex.Message}";
                Console.Error.WriteLine($"Errore save receipt: {ex}");
            }
            finally
            {
                _isSaving = false;
            }
        }
    }
}
