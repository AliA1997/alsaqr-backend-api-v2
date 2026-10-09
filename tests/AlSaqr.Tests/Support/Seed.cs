using Npgsql;

namespace AlSaqr.Tests.Support
{
    /// <summary>
    /// Inserts fixture rows with plain SQL (not through entity models), so tests
    /// control every column and never depend on the code under test to set up state.
    /// </summary>
    public sealed class Seed
    {
        private const string S = "\"alsaqr-2026\"";
        private readonly string _connectionString;

        public Seed(string connectionString) => _connectionString = connectionString;

        public Task<Guid> User(string username, string? avatar = null) =>
            Scalar<Guid>(
                $"insert into {S}.users (username, avatar) values (@username, @avatar) returning id",
                ("username", username), ("avatar", (object?)avatar ?? DBNull.Value));

        public Task<Guid> Group(Guid founderId) =>
            Scalar<Guid>(
                $"insert into {S}.groups (name, slug, founder_id) values ('Test Group', @slug, @founder) returning id",
                ("slug", $"group-{Guid.NewGuid():N}"), ("founder", founderId));

        public Task<Guid> Event(Guid? groupId, bool isOnline) =>
            Scalar<Guid>(
                $"insert into {S}.events (name, slug, group_id, is_online) values ('Test Event', @slug, @group, @online) returning id",
                ("slug", $"event-{Guid.NewGuid():N}"),
                ("group", (object?)groupId ?? DBNull.Value),
                ("online", isOnline));

        public Task<Guid> Attendee(Guid userId) =>
            Scalar<Guid>(
                $"insert into {S}.attendees (name, user_id) values ('Test Attendee', @user) returning id",
                ("user", userId));

        public Task<Guid> EventAttendee(Guid eventId, Guid attendeeId, Guid groupId, bool isOrganizer = false) =>
            Scalar<Guid>(
                $"insert into {S}.event_attendees (event_id, attendee_id, group_id, is_event_organizer) " +
                "values (@event, @attendee, @group, @organizer) returning id",
                ("event", eventId), ("attendee", attendeeId), ("group", groupId), ("organizer", isOrganizer));

        public async Task Execute(string sql, params (string Name, object Value)[] parameters)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = Command(sql, conn, parameters);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<T> Scalar<T>(string sql, params (string Name, object Value)[] parameters)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = Command(sql, conn, parameters);
            var result = await cmd.ExecuteScalarAsync();
            return result is null or DBNull ? default! : (T)result;
        }

        private static NpgsqlCommand Command(
            string sql, NpgsqlConnection conn, (string Name, object Value)[] parameters)
        {
            var cmd = new NpgsqlCommand(sql, conn);
            foreach (var (name, value) in parameters)
                cmd.Parameters.AddWithValue(name, value);
            return cmd;
        }
    }
}
