using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ufw.Web.Data.Extensions;

internal static class DbUpdateExceptionExtensions
{
    extension (DbUpdateException self)
    {
        public bool HasPostgresErrorCode(string sqlState) => self.InnerException is PostgresException
        {
            SqlState: { } state
        } && state.Equals(sqlState, StringComparison.Ordinal);

        public bool IsUniqueConstraintViolation => self.HasPostgresErrorCode(PostgresErrorCodes.UniqueViolation);

        public bool IsForeignKeyConstraintViolation => self.HasPostgresErrorCode(PostgresErrorCodes.ForeignKeyViolation);
    }
}
