using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.ExpenseReceipts
{
    [Authorize]
    public partial class Details : ComponentBase
    {
        [Parameter] public int InterventionId { get; set; }
        [Parameter] public int ReceiptId { get; set; }

        private ExpenseReceiptDTO _receipt;
        private bool _isLoading = true;
        private bool _isConfirming = false;
        private bool _isDeleting = false;

        protected override async Task OnInitializedAsync()
        {
            await LoadReceipt();
        }

        private async Task LoadReceipt()
        {
            try
            {
                _isLoading = true;
                _receipt = await Http.GetFromJsonAsync<ExpenseReceiptDTO>(
                    $"api/ExpenseReceipts/{ReceiptId}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore caricamento nota spese: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task ConfirmReceipt()
        {
            try
            {
                _isConfirming = true;

                var response = await Http.PostAsync($"api/ExpenseReceipts/{ReceiptId}/confirm", null);

                if (response.IsSuccessStatusCode)
                {
                    await LoadReceipt();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore conferma nota spese: {ex.Message}");
            }
            finally
            {
                _isConfirming = false;
            }
        }

        private async Task DeleteReceipt()
        {
            if (!await JSRuntime.InvokeAsync<bool>("confirm", "Sei sicuro di voler eliminare questa nota spese?"))
                return;

            try
            {
                _isDeleting = true;

                var response = await Http.DeleteAsync($"api/ExpenseReceipts/{ReceiptId}");

                if (response.IsSuccessStatusCode)
                {
                    NavigationManager.NavigateTo($"/TicketInterventions/{InterventionId}/ExpenseReceipts");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore eliminazione nota spese: {ex.Message}");
            }
            finally
            {
                _isDeleting = false;
            }
        }

        private string GetConfidenceClass(float confidence)
        {
            if (confidence >= 0.9f) return "bg-success";
            if (confidence >= 0.7f) return "bg-warning";
            return "bg-danger";
        }

        [Inject]
        private IJSRuntime JSRuntime { get; set; }
    }
}
