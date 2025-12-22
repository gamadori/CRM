using CRM.Shared.Helper;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM.Shared
{
    public class TaskProject
    {
       
        public TaskProject()
        {

        }

        public TaskProject(TaskData item)
        {
            this.Description = item.Description;
            this.Duration = item.Duration;
            this.End = item.End;
            this.Id = item.Id;
            this.Name = item.Name;
            this.ParentId = item.ParentId;
            this.Predecessor = item.Predecessor;
            this.Progress = item.Progress;
            this.Start = item.Start;
            this.IdProject = item.IdProject;
            this.IdTicketType = item.IdTicketType;

            
        }
        public int Id { get; set; }

        
        [ForeignKey("ProjectModel")]
        public int? IdProject { get; set; }

        [ForeignKey("TicketType")]
        public int? IdTicketType { get; set; }


        [Display(Name="Nome")]
        [Required]
        public string Name { get; set; }

        [Display(Name = "Descrizione")]
        public string Description { get; set; }

        [Display(Name = "Inizio Task")]
        public DateTime? Start { get; set; }

        [Display(Name = "Fine Task")]
        public DateTime? End { get; set; }

        [Display(Name = "Durata")]
        public string Duration { get; set; }

        [Display(Name = "Parent")]
        public int? ParentId { get; set; }

        [Display(Name = "Stato Avanzamento")]
        public int Progress { get; set; }

        public string Predecessor { get; set; }

        [NotMapped]
        public int IdTaskClone { get; set; }

        public virtual ProjectModel ProjectModel { get; set; }

        public virtual TicketType TicketType { get; set; }

        
    }


    public class TaskData
    {
        public TaskData()
        {

        }

        public TaskData(TaskProject item)
        {
            try
            {
                this.Id = item.Id;
                this.Name = item.Name;
                this.Description = item.Description;
                this.Duration = item.Duration;
                this.End = item.End.GetValueOrDefault();
                this.ParentId = item.ParentId;
                this.Predecessor = item.Predecessor;
                this.Progress = item.Progress;
                this.Start = item.Start.GetValueOrDefault();
                this.IdProject = item.IdProject;
                this.IdTicketType = item.IdTicketType;

                this.PredecessorList = new List<TaskDependency>();

                if (this.Predecessor.Trim().Length > 0)
                {
                    var predessors = this.Predecessor.Split(",");


                    foreach (var p in predessors)
                    {
                        var obj = GanttHelper.GetDependency(p);
                        
                        if (obj != null)
                            this.PredecessorList.Add(obj);
                    }
                }
                
               
            }
            catch
            {

            }
        }

        public TaskData(Ticket ticket)
        {
            this.Id = ticket.Id;
            this.Name = ticket.Description;
            this.Start = ticket.Date??DateTime.Now.Date;
            if (ticket.DateEnd != null)
            {
                this.End = ticket.DateEnd.Value;

                this.Duration = (ticket.DateEnd - ticket.Date).Value.TotalDays.ToString();
            }
            else
                this.Duration = "1";
            this.Progress = ticket.Progress;
            
        }
        public int Id { get; set; }

        [Required]
        [Display(Name = "Nome")]
        public string Name { get; set; }

        [Display(Name = "Progetto")]
        public int? IdProject { get; set; }

        [Display(Name = "Descrizione")]
        public string Description { get; set; }

        [Display(Name = "Data Iizio")]
        public DateTime Start { get; set; }
        [Display(Name = "Data Fine")]
        public DateTime End { get; set; }

        [Display(Name = "Durata")]
        public string Duration { get; set; }
        [Display(Name = "Pogresso")]
        public int Progress { get; set; }
        public string Predecessor { get; set; }

        public int? IdTicketType { get; set; }
        public string Notes { get; set; }
        public int? ParentId { get; set; }

        [NotMapped]
        public List<TaskDependency> PredecessorList { get; set; } 

        [NotMapped]
        public List<TaskData> SubTasks { get; set; }
    }

    public class TaskProjectFilter: PagingParameterModel
    {

        public string Name { get; set; }
        public string Description { get; set; }

        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }

        public string Duration { get; set; }

        public int? IdProject { get; set; }

    }

    public class TaskDataFilter: PagingParameterModel
    {
       
        public string Name { get; set; }

        public string Description { get; set; }
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        public string Predecessor { get; set; }
        public string Notes { get; set; }
        public int? ParentId { get; set; }

        public int? IdProject { get; set; }
    }

    public class TaskDataParent
    {
        public TaskDataParent()
        {

        }

        public TaskDataParent(TaskData data)
        {
            this.Id = data.Id;
            this.Name = data.Name;
        }
        public int? Id { get; set; }

        public string Name { get; set; }
    }

}
