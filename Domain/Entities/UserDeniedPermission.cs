namespace Domain.Entities
{
    public class UserDeniedPermission
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int PermissionId { get; set; }

        public User User { get; set; } = null!;
        public Permission Permission { get; set; } = null!;
    }
}
