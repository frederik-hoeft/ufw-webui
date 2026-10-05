using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Diagnostics.CodeAnalysis;
using Ufw.Web.Data.Access;

namespace Ufw.Web.Data.Extensions;

internal static class DbUpdateExceptionExtensions
{
    extension(DbUpdateException self)
    {
        public bool HasPostgresErrorCode(string sqlState) => self.InnerException is PostgresException
        {
            SqlState: { } state
        } && state.Equals(sqlState, StringComparison.Ordinal);

        public bool IsUniqueConstraintViolation => self.HasPostgresErrorCode(PostgresErrorCodes.UniqueViolation);

        public bool IsForeignKeyConstraintViolation => self.HasPostgresErrorCode(PostgresErrorCodes.ForeignKeyViolation);

        public bool TryGetDataMutationError([NotNullWhen(true)] out DataMutationError? error)
        {
            error = self.InnerException switch
            {
                PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } => new DataMutationUniqueConflictError(),
                PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } => new DataMutationReferenceConflictError(),
                _ => null,
            };
            return error is not null;
        }
    }
}
