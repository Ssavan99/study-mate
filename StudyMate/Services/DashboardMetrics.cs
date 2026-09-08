using StudyMate.Models;
using StudyMate.Models.ViewModels;
namespace StudyMate.Services;
public static class DashboardMetrics
{
 public static AdminDashboardViewModel Calculate(IEnumerable<Student> students,IEnumerable<StudyRequest> requests,IEnumerable<StudySession> sessions,IEnumerable<Report> reports,IEnumerable<Enrollment> enrollments)
 {
  var ss=students.ToList();var rr=requests.ToList();var se=sessions.ToList();var rp=reports.ToList();var fills=se.Where(s=>s.Capacity>0).Select(s=>100m*s.Participants.Count/s.Capacity).ToList();var resolved=rp.Where(r=>r.ReviewedAt.HasValue).Select(r=>(r.ReviewedAt!.Value-r.CreatedAt).TotalHours).OrderBy(x=>x).ToList();
  return new(){Students=ss.Count,VerifiedStudents=ss.Count(s=>s.EmailVerifiedAt.HasValue),RequestsSent=rr.Count,AcceptedRequests=rr.Count(r=>r.Status==RequestStatus.Accepted),SessionsCreated=se.Count,SessionJoins=se.Sum(s=>s.Participants.Count),AverageFillPercent=fills.Any()?decimal.Round(fills.Average(),1):0,OpenReports=rp.Count(r=>r.Status==ReportStatus.Open),MedianResolutionHours=resolved.Count==0?null:resolved[resolved.Count/2],ActiveCourses=enrollments.Where(e=>e.SeekingPartner).GroupBy(e=>e.Course?.Code??"Course").OrderByDescending(g=>g.Count()).Take(5).Select(g=>(g.Key,g.Count())).ToList()};
 }
}
