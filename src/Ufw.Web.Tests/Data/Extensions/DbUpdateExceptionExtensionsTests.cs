using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ufw.Web.Data.Access;
using Ufw.Web.Data.Extensions;

namespace Ufw.Web.Tests.Data.Extensions;

[TestClass]
public sealed class DbUpdateExceptionExtensionsTests
{
    [TestMethod]
    public void TryGetDataMutationError_UniqueViolationReturnsUniqueConflict()
    {
        DbUpdateException exception = Create(PostgresErrorCodes.UniqueViolation);

        bool mapped = exception.TryGetDataMutationError(out DataMutationError? error);

        Assert.IsTrue(mapped);
        Assert.IsInstanceOfType<DataMutationUniqueConflictError>(error);
    }

    [TestMethod]
    public void TryGetDataMutationError_ForeignKeyViolationReturnsReferenceConflict()
    {
        DbUpdateException exception = Create(PostgresErrorCodes.ForeignKeyViolation);

        bool mapped = exception.TryGetDataMutationError(out DataMutationError? error);

        Assert.IsTrue(mapped);
        Assert.IsInstanceOfType<DataMutationReferenceConflictError>(error);
    }

    [TestMethod]
    public void TryGetDataMutationError_UnclassifiedPostgresErrorIsNotConsumed()
    {
        DbUpdateException exception = Create(PostgresErrorCodes.CheckViolation);

        bool mapped = exception.TryGetDataMutationError(out DataMutationError? error);

        Assert.IsFalse(mapped);
        Assert.IsNull(error);
    }

    private static DbUpdateException Create(string sqlState) => new(
        "Database update failed.",
        new PostgresException("PostgreSQL rejected the update.", "ERROR", "ERROR", sqlState));
}
