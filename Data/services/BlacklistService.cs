using Data;
using Domain.Entities;
using Domain.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Data.Services
{
    public class BlacklistService
    {
        private readonly BankDbContext _context;

        public BlacklistService(BankDbContext context)
        {
            _context = context;
        }

        public async Task AddToBlacklistAsync(string token, DateTime expiry)
        {
            var revoked = new RevokedToken
            {
                Token = token,
                ExpiryDate = expiry
            };

            _context.RevokedTokens.Add(revoked);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> IsTokenRevokedAsync(string token)
        {
            return await _context.RevokedTokens.AnyAsync(r => r.Token == token);
        }
    }
}
