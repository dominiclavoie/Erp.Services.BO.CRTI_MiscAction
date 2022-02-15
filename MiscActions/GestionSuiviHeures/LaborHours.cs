using System;

namespace Erp.BO.CRTI_MiscAction.MiscActions.GestionSuiviHeures
{
    public class LaborHours
    {
		public string EmpID { get; set; }
		public DateTime DateStart { get; set; }
		public DateTime DateEnd { get; set; }
		public int WeekNumber { get; set; }
		public string OpCode { get; set; }
		public decimal Sunday { get; set; }
		public decimal Monday { get; set; }
		public decimal Tuesday { get; set; }
		public decimal Wednesday { get; set; }
		public decimal Thursday { get; set; }
		public decimal Friday { get; set; }
		public decimal Saturday { get; set; }
		public Guid RowID { get; set; }
		public LaborHours() { }
		public object[] GetValues()
		{
			return new object[] { EmpID, OpCode, DateStart, DateEnd, WeekNumber.ToString("D2"), Sunday, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, RowID };
		}
	}
}
