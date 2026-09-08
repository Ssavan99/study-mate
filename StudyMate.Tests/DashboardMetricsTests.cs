using StudyMate.Models;using StudyMate.Services;using Xunit;
namespace StudyMate.Tests;public class DashboardMetricsTests{[Fact]public void ZeroDataHasNoNan(){var m=DashboardMetrics.Calculate(Array.Empty<Student>(),Array.Empty<StudyRequest>(),Array.Empty<StudySession>(),Array.Empty<Report>(),Array.Empty<Enrollment>());Assert.Equal(0,m.AverageFillPercent);Assert.Null(m.MedianResolutionHours);}}
