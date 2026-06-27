using System;
using System.Collections.Generic;

namespace employee.management.identity.infrastructure;

public partial class RefreshToken
{
    public Guid Id { get; set; }

    public string Token { get; set; } = null!;

    public Guid IdentityUserId { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    public bool IsUsed { get; set; }

    public DateTime CreatedAt { get; set; }
}
