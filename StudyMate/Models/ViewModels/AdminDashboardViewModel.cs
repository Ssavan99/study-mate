namespace StudyMate.Models.ViewModels;
public class AdminDashboardViewModel
{
 public int Students { get; init; } public int VerifiedStudents { get; init; } public int RequestsSent { get; init; } public int AcceptedRequests { get; init; } public int SessionsCreated { get; init; } public int SessionJoins { get; init; } public decimal AverageFillPercent { get; init; } public int OpenReports { get; init; } public double? MedianResolutionHours { get; init; } public IReadOnlyList<(string Course, int Students)> ActiveCourses { get; init; } = Array.Empty<(string,int)>();
}
