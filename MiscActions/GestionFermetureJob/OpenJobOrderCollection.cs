using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Erp.BO.CRTI_MiscAction.MiscActions.GestionFermetureJob
{
    public class OpenJobOrderCollection
    {
        public IEnumerable<OpenJobOrder> openJobs { get; set; }
        public OpenJobOrderCollection() { }
        public IEnumerable<OpenJobOrderGroup> GroupByOrder()
        {
            return openJobs.GroupBy(s => new
            {
                s.OrderNum,
                s.CustomerName
            }).Select(g => new OpenJobOrderGroup
            {
                OrderNum = g.Key.OrderNum,
                CustomerName = g.Key.CustomerName,
                ReqDate = g.Min(tt => tt.ReqDate)
            });
        }
        public IEnumerable<OpenJobOrder> GetOrderDetail(int orderNum)
        {
            return openJobs.Where(s => s.OrderNum == orderNum);
        }
    }
}
