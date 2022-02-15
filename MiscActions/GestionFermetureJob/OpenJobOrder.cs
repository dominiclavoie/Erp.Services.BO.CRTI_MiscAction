using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Erp.BO.CRTI_MiscAction.MiscActions.GestionFermetureJob
{
    public class OpenJobOrder
    {
        public int OrderNum { get; set; }
        public int OrderLine { get; set; }
        public int OrderRelNum { get; set; }
        public string CustomerName { get; set; }
        public DateTime ReqDate { get; set; }
        public string JobNum { get; set; }
        public string PartNum { get; set; }
        public string OpCode { get; set; }
        public decimal RequiredQty { get; set; }
        public decimal RemainingQty { get; set; }
        public bool IsFinalOper { get; set; }
        public string ProdDetail { get; set; }
        public OpenJobOrder() { }
        public object[] GetValues() { return new object[] { false, OrderNum, OrderLine, OrderRelNum, CustomerName, ReqDate, JobNum, PartNum, OpCode, RequiredQty, RemainingQty, IsFinalOper, ProdDetail, Guid.NewGuid() }; }
    }
}
