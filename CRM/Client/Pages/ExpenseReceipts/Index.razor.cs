using CRM.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace CRM.Client.Pages.ExpenseReceipts
{
    [Authorize]
    public partial class Index : ComponentBase
    {
        [Parameter] public int InterventionId { get; set; }

        private List<ExpenseReceiptDTO> _receipts;
        private ExpenseReceiptSummaryDTO _summary;
        private bool _isLoading = true;
        private bool _isConfirming = false;

        protected override async Task OnInitializedAsync()
        {
            await LoadData();
        }

        private async Task LoadData()
        {
            try
            {
                _isLoading = true;

                var response = await Http.GetFromJsonAsync<ExpenseReceiptsResponse>(
                    $"api/ExpenseReceipts/intervention/{InterventionId}");

                if (response != null)
                {
                    _receipts = response.Receipts;
                    _summary = response.Summary;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Errore caricamento note spese: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private async Task ConfirmReceipt(int receiptId)
        {
            try
            {
                _isConfirming = true;

                var response = await Http.PostAsync($"api/ExpenseReceipts/{receiptId}/confirm", null);

                if (response.IsSuccessStatusCode)
                {
                    await LoadData();
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

        private class ExpenseReceiptsResponse
        {
            public List<ExpenseReceiptDTO> Receipts { get; set; }
            public ExpenseReceiptSummaryDTO Summary { get; set; }
        }
    }
}
