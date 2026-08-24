using Microsoft.EntityFrameworkCore;
using ResellerSystem.Server.Application.Security;
using ResellerSystem.Server.Data.Master;
using ResellerSystem.Server.Domain.Entities;

namespace ResellerSystem.Server.Data.Security;

public sealed class UserRepository : IUserRepository
{
    private readonly MasterDbContext _db;

    public UserRepository(MasterDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> AnyUsersExistAsync(CancellationToken ct = default) =>
        _db.Users.AnyAsync(ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync(ct);
    }
}
