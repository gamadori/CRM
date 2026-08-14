using BlazoringComponents.Models;
using Microsoft.AspNetCore.Components;
using System;
using System.Threading.Tasks;

namespace BlazoringComponents.Scheduler
{
    public class SchedulerTicketMoveArgs
    {
        public SchedulerTicket Ticket { get; set; }

        public DateTime Date { get; set; }

        public TimeOnly? Time { get; set; }

        public DateTime? DateEnd { get; set; }
    }

    public class SchedulerDragDropContext
    {
        public SchedulerTicket DraggedTicket { get; private set; }

        public EventCallback<SchedulerTicketMoveArgs> OnTicketMove { get; set; }

        public void Start(SchedulerTicket ticket)
        {
            DraggedTicket = ticket;
        }

        public void Clear()
        {
            DraggedTicket = null;
        }

        public async Task DropAsync(DateTime date, TimeOnly? time)
        {
            if (DraggedTicket == null || !OnTicketMove.HasDelegate)
                return;

            var dropHasTime = time.HasValue;
            var effectiveTime = time ?? DraggedTicket.TimeStart;
            var newStart = date.Date + (effectiveTime?.ToTimeSpan() ?? TimeSpan.Zero);
            var oldStart = DraggedTicket.DateStart.Date + (DraggedTicket.TimeStart?.ToTimeSpan() ?? TimeSpan.Zero);
            var duration = DraggedTicket.HasExplicitEnd && DraggedTicket.DateEnd > oldStart
                ? DraggedTicket.DateEnd - oldStart
                : TimeSpan.FromHours(1);

            await OnTicketMove.InvokeAsync(new SchedulerTicketMoveArgs
            {
                Ticket = DraggedTicket,
                Date = date.Date,
                Time = effectiveTime,
                DateEnd = dropHasTime || DraggedTicket.IsScheduled
                    ? newStart.Add(duration)
                    : null
            });

            Clear();
        }
    }
}
