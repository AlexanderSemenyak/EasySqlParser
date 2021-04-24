﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Dapper;
using EasySqlParser.SqlGenerator;
using EasySqlParser.SqlGenerator.Configurations;
using EasySqlParser.SqlGenerator.Enums;
using EasySqlParser.SqlGenerator.Helpers;

namespace EasySqlParser.Dapper.Extensions
{
    public static class DapperExtension
    {
        public static DynamicParameters ToDynamicParameters(this IReadOnlyList<IDbDataParameter> parameters)
        {
            var result = new DynamicParameters();
            foreach (var parameter in parameters)
            {

                result.Add(parameter.ParameterName,
                    parameter.Value,
                    parameter.DbType,
                    parameter.Direction,
                    parameter.Size,
                    parameter.Precision,
                    parameter.Scale);
            }

            return result;
        }

        public static T ExecuteReaderSingle<T>(this DbConnection connection,
            IQueryBuilderConfiguration configuration,
            Expression<Func<T, bool>> predicate,
            DbTransaction transaction = null)
            where T : class
        {
            var builderResult = QueryBuilder.GetSelectSql(configuration, predicate);
            configuration.LoggerAction?.Invoke(builderResult.DebugSql);
            return connection.QuerySingle<T>(builderResult.ParsedSql,
                builderResult.DbDataParameters.ToDynamicParameters(), transaction);
        }

        public static async Task<T> ExecuteReaderSingleAsync<T>(this DbConnection connection,
            IQueryBuilderConfiguration configuration,
            Expression<Func<T, bool>> predicate,
            DbTransaction transaction = null)
            where T : class
        {
            var builderResult = QueryBuilder.GetSelectSql(configuration, predicate);
            configuration.LoggerAction?.Invoke(builderResult.DebugSql);
            return await connection.QuerySingleAsync<T>(builderResult.ParsedSql,
                builderResult.DbDataParameters.ToDynamicParameters(), transaction).ConfigureAwait(false);
        }

        public static IEnumerable<T> ExecuteReader<T>(this DbConnection connection,
            IQueryBuilderConfiguration configuration,
            Expression<Func<T, bool>> predicate,
            DbTransaction transaction = null)
            where T : class
        {
            var builderResult = QueryBuilder.GetSelectSql(configuration, predicate);
            configuration.LoggerAction?.Invoke(builderResult.DebugSql);
            return connection.Query<T>(builderResult.ParsedSql,
                builderResult.DbDataParameters.ToDynamicParameters(), transaction);
        }

        public static async Task<IEnumerable<T>> ExecuteReaderAsync<T>(this DbConnection connection,
            IQueryBuilderConfiguration configuration,
            Expression<Func<T, bool>> predicate,
            DbTransaction transaction = null)
            where T : class
        {
            var builderResult = QueryBuilder.GetSelectSql(configuration, predicate);
            configuration.LoggerAction?.Invoke(builderResult.DebugSql);
            return await connection.QueryAsync<T>(builderResult.ParsedSql,
                builderResult.DbDataParameters.ToDynamicParameters(), transaction).ConfigureAwait(false);
        }

        public static int GetCount<T>(this DbConnection connection,
            IQueryBuilderConfiguration configuration,
            Expression<Func<T, bool>> predicate,
            DbTransaction transaction = null)
            where T : class
        {
            var builderResult = QueryBuilder.GetCountSql(predicate, configuration);
            configuration.LoggerAction?.Invoke(builderResult.DebugSql);
            return connection.ExecuteScalar<int>(builderResult.ParsedSql,
                builderResult.DbDataParameters.ToDynamicParameters(), transaction);
        }

        public static async Task<int> GetCountAsync<T>(this DbConnection connection,
            IQueryBuilderConfiguration configuration,
            Expression<Func<T, bool>> predicate,
            DbTransaction transaction = null)
            where T : class
        {
            var builderResult = QueryBuilder.GetCountSql(predicate, configuration);
            configuration.LoggerAction?.Invoke(builderResult.DebugSql);
            return await connection.ExecuteScalarAsync<int>(builderResult.ParsedSql,
                builderResult.DbDataParameters.ToDynamicParameters(), transaction).ConfigureAwait(false);
        }


        public static async Task<int> ExecuteAsync(this DbConnection connection,
            QueryBuilderParameter builderParameter,
            DbTransaction transaction = null)
        {
            await SequenceHelper.GenerateAsync(connection, builderParameter).ConfigureAwait(false);
            var builderResult = QueryBuilder.GetQueryBuilderResult(builderParameter);
            builderParameter.WriteLog(builderResult.DebugSql);
            int affectedCount;
            builderParameter.SaveExpectedVersion();
            switch (builderParameter.CommandExecutionType)
            {
                case CommandExecutionType.ExecuteNonQuery:
                    affectedCount =
                        await ConsumeHelper.ConsumeNonQueryAsync(connection, builderParameter, builderResult, transaction)
                            .ConfigureAwait(false);
                    break;
                case CommandExecutionType.ExecuteReader:
                    affectedCount =
                        await ConsumeHelper.ConsumeReaderAsync(connection, builderParameter, builderResult, transaction)
                            .ConfigureAwait(false);
                    break;
                case CommandExecutionType.ExecuteScalar:
                    affectedCount =
                        await ConsumeHelper.ConsumeScalarAsync(connection, builderParameter, builderResult, transaction)
                            .ConfigureAwait(false);
                    break;
                default:
                    // TODO: error
                    throw new InvalidOperationException("");
            }

            ThrowIfOptimisticLockException(builderParameter, affectedCount, builderResult, transaction);
            builderParameter.IncrementVersion();
            return affectedCount;


        }

        public static int Execute(this DbConnection connection,
            QueryBuilderParameter builderParameter,
            DbTransaction transaction = null)
        {
            SequenceHelper.Generate(connection, builderParameter);
            var builderResult = QueryBuilder.GetQueryBuilderResult(builderParameter);
            builderParameter.WriteLog(builderResult.DebugSql);

            int affectedCount;
            builderParameter.SaveExpectedVersion();

            switch (builderParameter.CommandExecutionType)
            {
                case CommandExecutionType.ExecuteNonQuery:
                    affectedCount =
                        ConsumeHelper.ConsumeNonQuery(connection, builderParameter, builderResult, transaction);
                    break;
                case CommandExecutionType.ExecuteReader:
                    affectedCount =
                        ConsumeHelper.ConsumeReader(connection, builderParameter, builderResult, transaction);
                    break;
                case CommandExecutionType.ExecuteScalar:
                    affectedCount =
                        ConsumeHelper.ConsumeScalar(connection, builderParameter, builderResult, transaction);
                    break;
                default:
                    // TODO: error
                    throw new InvalidOperationException("");
            }

            ThrowIfOptimisticLockException(builderParameter, affectedCount, builderResult, transaction);
            builderParameter.IncrementVersion();
            return affectedCount;

        }


        private static void ThrowIfOptimisticLockException(
            QueryBuilderParameter parameter,
            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            int affectedCount,
            QueryBuilderResult builderResult,
            DbTransaction transaction)
        {
            if (parameter.ThrowableOptimisticLockException(affectedCount))
            {
                transaction?.Rollback();
                throw new OptimisticLockException(builderResult.ParsedSql, builderResult.DebugSql, parameter.SqlFile);
            }
        }
    }

    internal static class ConsumeHelper
    {
        private static DbCommand BuildCommand(DbConnection connection,
            QueryBuilderParameter builderParameter,
            QueryBuilderResult builderResult,
            DbTransaction transaction = null)
        {
            var command = connection.CreateCommand();
            command.CommandText = builderResult.ParsedSql;
            command.Parameters.AddRange(builderResult.DbDataParameters.ToArray());
            if (transaction != null)
            {
                command.Transaction = transaction;
            }
            command.CommandTimeout = builderParameter.CommandTimeout;
            return command;
        }

        internal static int ConsumeNonQuery(DbConnection connection,
            QueryBuilderParameter builderParameter,
            QueryBuilderResult builderResult,
            DbTransaction transaction = null)
        {
            // returning value not working in dapper
            //var affectedCount = connection.Execute(
            //    builderResult.ParsedSql,
            //    builderResult.DbDataParameters.ToDynamicParameters(),
            //    transaction,
            //    builderParameter.CommandTimeout
            //);
            
            ////builderParameter.ApplyReturningColumns();
            //return affectedCount;
            using (var command = BuildCommand(connection, builderParameter, builderResult, transaction))
            {
                var affectedCount = command.ExecuteNonQuery();
                builderParameter.ApplyReturningColumns();
                return affectedCount;
            }
        }

        internal static async Task<int> ConsumeNonQueryAsync(DbConnection connection,
            QueryBuilderParameter builderParameter,
            QueryBuilderResult builderResult,
            DbTransaction transaction = null)
        {
            //var affectedCount = await connection.ExecuteAsync(
            //    builderResult.ParsedSql,
            //    builderResult.DbDataParameters.ToDynamicParameters(),
            //    transaction,
            //    builderParameter.CommandTimeout)
            //    .ConfigureAwait(false);
            //return affectedCount;
            using (var command = BuildCommand(connection, builderParameter, builderResult, transaction))
            {
                var affectedCount = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                builderParameter.ApplyReturningColumns();
                return affectedCount;
            }
        }

        internal static int ConsumeReader(IDbConnection connection,
            QueryBuilderParameter builderParameter,
            QueryBuilderResult builderResult,
            DbTransaction transaction = null)
        {
            var entity = connection.QuerySingleOrDefault(builderParameter.Entity.GetType(),
                builderResult.ParsedSql,
                builderResult.DbDataParameters.ToDynamicParameters(),
                transaction,
                builderParameter.CommandTimeout);
            if (entity == null) return 0;
            builderParameter.ResetEntity(entity);
            if (!builderParameter.IsSameVersion())
            {
                return 0;
            }
            return 1;

        }

        internal static async Task<int> ConsumeReaderAsync(IDbConnection connection,
            QueryBuilderParameter builderParameter,
            QueryBuilderResult builderResult,
            DbTransaction transaction = null)
        {
            var entity = await connection.QuerySingleOrDefaultAsync(builderParameter.EntityType,
                builderResult.ParsedSql,
                builderResult.DbDataParameters.ToDynamicParameters(),
                transaction,
                builderParameter.CommandTimeout)
                .ConfigureAwait(false);
            if (entity == null) return 0;
            builderParameter.ResetEntity(entity);
            if (!builderParameter.IsSameVersion())
            {
                return 0;
            }

            return 1;
        }

        internal static int ConsumeScalar(IDbConnection connection,
            QueryBuilderParameter builderParameter,
            QueryBuilderResult builderResult,
            DbTransaction transaction = null)
        {
            var rawScalarValue = connection.ExecuteScalar(
                builderResult.ParsedSql,
                builderResult.DbDataParameters.ToDynamicParameters(),
                transaction,
                builderParameter.CommandTimeout);
            return DbCommandHelper.ConsumeScalar(rawScalarValue, builderParameter);
        }

        internal static async Task<int> ConsumeScalarAsync(IDbConnection connection,
            QueryBuilderParameter builderParameter,
            QueryBuilderResult builderResult,
            DbTransaction transaction = null)
        {
            var rawScalarValue = await connection.ExecuteScalarAsync(
                builderResult.ParsedSql,
                builderResult.DbDataParameters.ToDynamicParameters(),
                transaction,
                builderParameter.CommandTimeout)
                .ConfigureAwait(false);

            return DbCommandHelper.ConsumeScalar(rawScalarValue, builderParameter);
        }
    }

}
