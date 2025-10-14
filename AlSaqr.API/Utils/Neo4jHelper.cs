using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neo4j.Driver;
using Newtonsoft.Json;

namespace AlSaqr.API.Utils
{

    public static class Neo4jHelpers
    {
        public static async Task<List<Dictionary<string, object>>> ReadAsync(
                IAsyncSession session,
                string cypher,
                IDictionary<string, object> parameters,
                IEnumerable<string>? aliases = null,
                string? countQuery = null
        )
        {
            try
            {
                var resultCursor = await session.RunAsync(cypher, parameters);
                var records = await resultCursor.ToListAsync();

                return records.Select(record =>
                {
                    if (aliases != null && aliases.Any())
                    {
                        var result = new Dictionary<string, object>();
                        foreach (var alias in aliases)
                        {
                            var recordValue = record[alias];

                            if (recordValue == null)
                                result[alias] = null!;
                            else if (recordValue is INode node)
                                result[alias] = node.Properties;
                            else if (recordValue is IRelationship rel)
                                result[alias] = rel.Properties;
                            else if (recordValue is List<INode> nodes)
                                result[alias] = nodes.Select(n => n.Properties).ToList();
                            else if (recordValue is long l)
                                result[alias] = l;
                            else if (recordValue is int i)
                                result[alias] = i;
                            else if (recordValue is DateTime dt)
                                result[alias] = dt.ToString("yyyy-MM-dd HH:mm:ss"); // adjust like convertDateToDisplay
                            else
                                result[alias] = recordValue;
                        }
                        return result;
                    }

                    // Default alias
                    var defaultAlias = aliases?.FirstOrDefault() ?? "u";
                    var defaultValue = record[defaultAlias];

                    if (defaultAlias == "total" && defaultValue is long countValue)
                        return new Dictionary<string, object> { { "total", countValue } };

                    if (defaultValue is INode defaultNode)
                        return defaultNode.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    return new Dictionary<string, object> { { defaultAlias, defaultValue } };
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReadAsync (aliases: {string.Join(",", aliases ?? new List<string>())}): {ex}");
                return new List<Dictionary<string, object>>();
            }
            finally
            {
                Console.WriteLine("Successfully Read Data");
            }
        }


        public static async Task<List<Dictionary<string, object>>> ReadNestedAsync(
            IAsyncSession session,
            string cypher = "",
            object? parameters = null,
            IEnumerable<string>? aliases = null,
            string nestedAliasKey = "",
            IEnumerable<string>? nestedAliases = null
        )
        {
            try
            {
                var resultCursor = await session.RunAsync(cypher, parameters as IDictionary<string, object> ?? new Dictionary<string, object>());
                var records = await resultCursor.ToListAsync();

                return records.Select(record =>
                {
                    if (aliases != null && aliases.Any())
                    {
                        var result = new Dictionary<string, object>();

                        foreach (var alias in aliases)
                        {
                            var recordValue = record[alias];

                            if (alias == nestedAliasKey && recordValue is INode nestedNode)
                            {
                                var nestedDict = new Dictionary<string, object>(nestedNode.Properties);

                                if (nestedAliases != null)
                                {
                                    foreach (var nAlias in nestedAliases)
                                    {
                                        if (nestedDict.ContainsKey(nAlias) && nestedDict[nAlias] is INode innerNode)
                                        {
                                            nestedDict[nAlias] = innerNode.Properties;
                                        }
                                    }
                                }
                                result[alias] = nestedDict;
                            }
                            else if (recordValue is INode node)
                            {
                                result[alias] = node.Properties;
                            }
                            else if (recordValue is List<INode> nodes)
                            {
                                result[alias] = nodes.Select(n => n.Properties).ToList();
                            }
                            else
                            {
                                result[alias] = recordValue;
                            }
                        }
                        return result;
                    }

                    var defaultAlias = aliases?.FirstOrDefault() ?? "u";
                    if (record[defaultAlias] is INode defNode)
                        return defNode.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    return new Dictionary<string, object> { { defaultAlias, record[defaultAlias] } };
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ReadNestedAsync (aliases: {string.Join(",", aliases ?? new List<string>())}): {ex}");
                return new List<Dictionary<string, object>>();
            }
            finally
            {
                Console.WriteLine("Successfully Read Nested Data");
            }
        }

        public static async Task WriteAsync(
            IAsyncSession session,
            string cypher,
            IDictionary<string, object> parameters
        )
        {
            try
            {
                await session.ExecuteWriteAsync(async tx =>
                {
                    var result = await tx.RunAsync(
                        cypher,
                        parameters
                    );
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in WriteAsync: {ex}");
            }
            finally
            {
                Console.WriteLine("Successfully Write Data");
            }
        }

        public static async Task<string?> GetUserIdFromSessionAsync(
            IAsyncSession session,
            string userEmail
        )
        {
            if (string.IsNullOrEmpty(userEmail))
                throw new Exception("Can't perform this request without being logged in.");

            var result = await ReadAsync(
                session,
                @"
                MATCH (user:User {email: $email})
                WITH user.id as id
                RETURN id
            ",
                new  Dictionary<string, object>() 
                {
                    { "email", userEmail }
                },
                new[] { "id" }
            );

            if (result != null && result.Any() && result[0].ContainsKey("id"))
                return result[0]["id"].ToString();

            return null;
        }


        public static string CommonCountCipher(string query, string alias)
        {
            return $@"CALL {{
            {query}
        }}
        RETURN count(DISTINCT {alias}) as total";
        }
    }

}
