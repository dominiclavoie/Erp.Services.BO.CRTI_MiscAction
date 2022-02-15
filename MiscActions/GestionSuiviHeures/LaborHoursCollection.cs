using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Erp.BO.CRTI_MiscAction.MiscActions.GestionSuiviHeures
{
    internal enum LaborHoursType
    {
        Hebdomadaire,
        Mensuelle,
        Annuelle
    }
    public class LaborHoursCollection
    {
        private IEnumerable<LaborHours> _laborHours { get; set; }
        public LaborHoursCollection(IEnumerable<LaborHours> laborHours)
        {
            _laborHours = laborHours;
        }
        public IEnumerable<LaborHours> GetLaborHours(string laborHoursType, DateTime date)
        {
            LaborHoursType _laborHoursType;
            if (!Enum.TryParse(laborHoursType, out _laborHoursType)) return _laborHours;
            switch (_laborHoursType)
            {
                case LaborHoursType.Hebdomadaire:
                    return FilterEmployeeWithLaborHours(GetWeeklyLaborHours(date));

                case LaborHoursType.Mensuelle:
                    return FilterEmployeeWithLaborHours(GetMonthlyLaborHours(date));

                case LaborHoursType.Annuelle:
                    return FilterEmployeeWithLaborHours(_laborHours);

                default: return _laborHours;
            }
        }

        private IEnumerable<LaborHours> FilterEmployeeWithLaborHours(IEnumerable<LaborHours> laborHours)
        {
            List<string> empIDs = laborHours.GroupBy(g => new
                {
                    g.EmpID
                }).Select(s => new
                {
                    s.Key.EmpID,
                    Hours = s.Sum(tt => tt.Sunday + tt.Monday + tt.Tuesday + tt.Wednesday + tt.Thursday + tt.Friday + tt.Saturday)
                }).Where(w => w.Hours > 0m)
                .Select(s => s.EmpID).ToList();

            List<string> except = laborHours.GroupBy(g => new
                {
                    g.WeekNumber,
                    g.EmpID
                }).Select(s => new
                {
                    s.Key.WeekNumber,
                    s.Key.EmpID,
                    Hours = s.Sum(tt => tt.Sunday + tt.Monday + tt.Tuesday + tt.Wednesday + tt.Thursday + tt.Friday + tt.Saturday)
                }).Where(w => w.Hours > 0m)
                  .Select(s => s.WeekNumber.ToString() + s.EmpID).ToList();
            

            return laborHours.Where(tt => empIDs.Contains(tt.EmpID) &&
                                          ((except.Contains(tt.WeekNumber.ToString() + tt.EmpID) && !string.IsNullOrEmpty(tt.OpCode)) ||
                                          !except.Contains(tt.WeekNumber.ToString() + tt.EmpID))).OrderBy(o => o.WeekNumber).ThenBy(t => t.EmpID);
        }

        private IEnumerable<LaborHours> GetWeeklyLaborHours(DateTime date)
        {
            return _laborHours.Where( tt => tt.DateStart <= date && tt.DateEnd >= date );
        }

        private IEnumerable<LaborHours> GetMonthlyLaborHours(DateTime date)
        {
            int year = date.Year;
            int month = date.Month;
            int nextMonthYear = date.AddMonths(1).Year;
            int nextMonthMonth = date.AddMonths(1).Month;
            DateTime dateStart = DayCollection.GetFirstWeekOfMonthDate(year, month);
            DateTime dateEnd = DayCollection.GetFirstWeekOfMonthDate(nextMonthYear, nextMonthMonth).AddDays(-1);
            return _laborHours.Where(tt => tt.DateStart == dateStart ||
                                           tt.DateEnd == dateEnd ||
                                           (tt.DateStart > dateStart && tt.DateEnd < dateEnd));
        }
    }
}
