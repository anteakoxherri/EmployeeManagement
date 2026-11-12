namespace EmployeeManagement.Server.DTOs
{
    // PUT riperdor validimin e fushave te krijimit dhe perfshin gjendjen e detyres.
    public class UpdateTaskDto : CreateTaskDto
    {
        // Kur nuk dergohet, gjendja ekzistuese ruhet; false e rihap shprehimisht detyren.
        public bool? IsCompleted { get; set; }
    }
}
