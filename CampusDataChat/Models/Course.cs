namespace CampusDataChat.Models;

public sealed record Course(
int Id,
string Code,
string Title,
string Department,
int Enrollment,
double AverageGrade,
string DeliveryMode);
