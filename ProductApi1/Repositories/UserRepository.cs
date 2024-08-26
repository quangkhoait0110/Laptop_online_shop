using ProductApi1.Models;
using Couchbase;
using Couchbase.Extensions.DependencyInjection;
using Couchbase.Query;

namespace ProductApi1.Repositories
{
    public class UserRepository
    {
        private readonly IBucket _bucket;

        public UserRepository(IBucketProvider bucketProvider)
        {
            try
            {
                _bucket = bucketProvider.GetBucketAsync("sohoa").Result;
            }
            catch (Exception ex)
            {
                // Log the exception
                throw new Exception("Failed to connect to Couchbase bucket", ex);
            }
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                var query = await _bucket.Cluster.QueryAsync<User>(
                    "SELECT * FROM `sohoa` WHERE email = $email",
                    options => options.Parameter("email", email));

                return await query.Rows.FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                // Log the exception
                throw new Exception($"Failed to get user by email: {email}", ex);
            }
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            try
            {
                var query = await _bucket.Cluster.QueryAsync<User>(
                    "SELECT * FROM `sohoa` WHERE username = $username",
                    options => options.Parameter("username", username));

                return await query.Rows.FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                // Log the exception
                throw new Exception($"Failed to get user by username: {username}", ex);
            }
        }
              public async Task<User> GetUserByUsernameAsync2(string username)
        {
            var query = await _bucket.Cluster.QueryAsync<UserWrapper>(
                "SELECT * FROM sohoa WHERE sohoa.username = $username",
                options => options.Parameter("username", username));

            var result = await query.Rows.FirstOrDefaultAsync();
            return result?.sohoa ?? throw new KeyNotFoundException($"User with username {username} not found.");
        }
        public async Task CreateUserAsync(User user)
        {
            try
            {
                await _bucket.DefaultCollection().InsertAsync(user.Id!, user);
            }
            catch (Exception ex)
            {
                // Log the exception
                throw new Exception($"Failed to create user: {user.Id}", ex);
            }
        }
    }
}