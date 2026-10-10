using Supabase.Postgrest.Exceptions;

namespace AlSaqr.Data.Helpers
{
    /// <summary>
    /// Classifies PostgREST errors by the code in PostgREST's JSON error body
    /// (<see cref="PostgrestException.Content"/>).
    /// </summary>
    public static class PostgrestErrors
    {
        /// <summary>PostgreSQL 23505: unique_violation.</summary>
        public static bool IsUniqueViolation(PostgrestException ex) => HasCode(ex, "23505");

        /// <summary>
        /// The table is not deployed: PostgreSQL 42P01 (undefined_table), or PGRST205
        /// when PostgREST's schema cache already knows the table is missing.
        /// </summary>
        public static bool IsMissingTable(PostgrestException ex) => HasCode(ex, "42P01") || HasCode(ex, "PGRST205");

        private static bool HasCode(PostgrestException ex, string code) =>
            ex.Content?.Contains($"\"code\":\"{code}\"", StringComparison.Ordinal) == true;
    }
}
