using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRM.Shared
{

    public class PagingResponse<T> 
    {
        public List<T> Items { get; set; }

        public PagingHeaderModel MetaData { get; set; }

        public string Total { get; set; }
    }

    public class PagingResponse<T, S>
    {
        public List<T> Items { get; set; }

        public PagingHeaderModel MetaData { get; set; }

        public S Total { get; set; }
    }


    public class PagingRequest<T> where T: class, new()
    {
        public PagingRequest()
        {
            Filter = new T();
            MetaData = new PagingParameterModel();
        }
        public T Filter { get; set; }

        public PagingParameterModel MetaData { get; set; }

    }
    
    public class PagingParameterModel
    {
        private const int _maxPageSize = 20;

        private int _pageSize = 10;

        public int PageNumber { get; set; } = 1;

        public int? Skip { get; set; } = null;

        public int? Top { get; set; } = null;

        public string Filter { get; set; }

        public string OrderBy { get; set; }
      

        public int PageSize 
        { 
            get { return _pageSize; }
            set 
            {
                _pageSize = (value > _maxPageSize) ? _maxPageSize : value;
            }
        }
    }

    public class PagingHeaderModel
    {
        public int TotalCount { get; set; }
        public int PageSize { get; set; }

        public int TotalPage { get; set; }

        public bool PreviousPage { get; set; }

        public bool NextPage { get; set; }

    }
}
