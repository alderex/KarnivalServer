using Microsoft.Data.Sqlite;

public sealed class SqliteAccountPersistence
{
    private readonly string databasePath;

    public SqliteAccountPersistence(string databasePath)
    {
        this.databasePath = ResolvePath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(this.databasePath) ?? ".");
        InitializeSchema();
    }

    public AccountLoginRecord? TryGetOrCreateLogin(string username, string loginHash)
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();

        DateTime now = DateTime.UtcNow;
        AccountLoginRecord? existing = FindUserByUsername(connection, transaction, username);
        if (existing != null)
        {
            if (!string.Equals(existing.LoginHash, loginHash, StringComparison.Ordinal))
                return null;

            using SqliteCommand updateUser = connection.CreateCommand();
            updateUser.Transaction = transaction;
            updateUser.CommandText = """
                UPDATE users
                SET last_login_at = $lastLoginAt
                WHERE id = $id;
                """;
            updateUser.Parameters.AddWithValue("$lastLoginAt", now.ToString("O"));
            updateUser.Parameters.AddWithValue("$id", existing.UserId);
            updateUser.ExecuteNonQuery();
            transaction.Commit();
            return existing;
        }

        using SqliteCommand insertUser = connection.CreateCommand();
        insertUser.Transaction = transaction;
        insertUser.CommandText = """
            INSERT INTO users (username, login_hash, created_at, last_login_at, wins)
            VALUES ($username, $loginHash, $createdAt, $lastLoginAt, 0);
            SELECT last_insert_rowid();
            """;
        insertUser.Parameters.AddWithValue("$username", username);
        insertUser.Parameters.AddWithValue("$loginHash", loginHash);
        insertUser.Parameters.AddWithValue("$createdAt", now.ToString("O"));
        insertUser.Parameters.AddWithValue("$lastLoginAt", now.ToString("O"));
        long userId = Convert.ToInt64(insertUser.ExecuteScalar());
        transaction.Commit();
        return new AccountLoginRecord(userId, username, loginHash, 0);
    }

    public void IncrementWins(IEnumerable<long> userIds)
    {
        long[] distinctUserIds = userIds.Distinct().ToArray();
        if (distinctUserIds.Length == 0)
            return;

        using SqliteConnection connection = OpenConnection();
        using SqliteTransaction transaction = connection.BeginTransaction();
        foreach (long userId in distinctUserIds)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE users SET wins = wins + 1 WHERE id = $id;";
            command.Parameters.AddWithValue("$id", userId);
            if (command.ExecuteNonQuery() != 1)
                throw new InvalidOperationException($"Could not increment wins for user {userId}.");
        }

        transaction.Commit();
    }
    public void ResetDatabase()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM users;
            DELETE FROM sqlite_sequence WHERE name = 'users';
            """;
        command.ExecuteNonQuery();
    }

    private void InitializeSchema()
    {
        using SqliteConnection connection = OpenConnection();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT NOT NULL UNIQUE CHECK(length(username) BETWEEN 1 AND 32),
                login_hash TEXT NOT NULL CHECK(length(login_hash) = 64),
                created_at TEXT NOT NULL,
                last_login_at TEXT NOT NULL,
                wins INTEGER NOT NULL DEFAULT 0
            );

            CREATE UNIQUE INDEX IF NOT EXISTS idx_users_username ON users(username);
            """;
        command.ExecuteNonQuery();
        EnsureWinsColumn(connection);
    }

    private static void EnsureWinsColumn(SqliteConnection connection)
    {
        using SqliteCommand inspect = connection.CreateCommand();
        inspect.CommandText = "PRAGMA table_info(users);";
        using SqliteDataReader reader = inspect.ExecuteReader();
        bool hasWins = false;
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), "wins", StringComparison.OrdinalIgnoreCase))
            {
                hasWins = true;
                break;
            }
        }

        reader.Close();
        if (hasWins)
            return;

        using SqliteCommand migrate = connection.CreateCommand();
        migrate.CommandText = "ALTER TABLE users ADD COLUMN wins INTEGER NOT NULL DEFAULT 0;";
        migrate.ExecuteNonQuery();
    }

    private AccountLoginRecord? FindUserByUsername(SqliteConnection connection, SqliteTransaction transaction, string username)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT id, username, login_hash, wins FROM users WHERE username = $username LIMIT 1;";
        command.Parameters.AddWithValue("$username", username);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        return new AccountLoginRecord(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3));
    }

    private SqliteConnection OpenConnection()
    {
        SqliteConnection connection = new($"Data Source={databasePath}");
        connection.Open();
        return connection;
    }

    private static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        return Path.Combine(AppContext.BaseDirectory, path);
    }
}

public sealed class AccountLoginRecord
{
    public AccountLoginRecord(long userId, string username, string loginHash, int wins)
    {
        UserId = userId;
        Username = username;
        LoginHash = loginHash;
        Wins = wins;
    }

    public long UserId { get; }
    public string Username { get; }
    public string LoginHash { get; }
    public int Wins { get; }
}