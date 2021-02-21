﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using EasySqlParser.Configurations;
using EasySqlParser.Extensions;

namespace EasySqlParser.SqlGenerator
{
    public class QueryStringBuilder
    {
        private readonly StringBuilder _rawSqlBuilder = new StringBuilder(200);
        private readonly StringBuilder _formattedSqlBuilder = new StringBuilder(200);

        private readonly SqlParserConfig _config;
        private readonly bool _writeIndented;
        private readonly Dictionary<string, IDbDataParameter> _sqlParameters =
            new Dictionary<string, IDbDataParameter>();

        private string _indent = "";
        private bool _firstWord;

        public QueryStringBuilder(SqlParserConfig config, bool writeIndented)
        {
            _config = config;
            _writeIndented = writeIndented;
            _firstWord = true;
        }


        public void ApplyIndent(int length)
        {
            if (_config.SupportsFinalTable())
            {
                _indent = "".PadLeft(length, ' ');
            }
        }

        public void RemoveIndent()
        {
            _indent = "";
        }

        public void AppendSql(string sql)
        {
            if (_writeIndented && _firstWord)
            {
                _rawSqlBuilder.Append(_indent);
                _formattedSqlBuilder.Append(_indent);
                _firstWord = false;
            }
            _rawSqlBuilder.Append(sql);
            _formattedSqlBuilder.Append(sql);
        }

        public void AppendLine()
        {
            if (!_writeIndented) return;
            _firstWord = true;
            _rawSqlBuilder.AppendLine();
            _formattedSqlBuilder.AppendLine();
        }

        public void AppendLine(string sql)
        {
            if (!_writeIndented)
            {
                _rawSqlBuilder.Append(sql);
                _formattedSqlBuilder.Append(sql);
                return;
            }
            if (_firstWord)
            {
                _rawSqlBuilder.Append(_indent);
                _formattedSqlBuilder.Append(_indent);
            }
            _rawSqlBuilder.AppendLine(sql);
            _formattedSqlBuilder.AppendLine(sql);
        }

        public void AppendParameter(string parameterKey, object parameterValue)
        {
            var (parameterName, parameter) = _config.CreateDbParameter(parameterKey, parameterValue);
            if (!_sqlParameters.ContainsKey(parameterKey))
            {
                _sqlParameters.Add(parameterKey, parameter);
            }
            _rawSqlBuilder.Append(parameterName);
            _formattedSqlBuilder.Append(_config.ConvertToLogFormat(parameterValue));
        }

        public void AppendComma(int counter)
        {
            if (_writeIndented && _firstWord)
            {
                _rawSqlBuilder.Append(_indent);
                _formattedSqlBuilder.Append(_indent);
                _firstWord = false;
            }
            if (counter == 0)
            {
                if (_writeIndented)
                {
                    AppendSql("   ");
                }
            }
            else
            {
                AppendSql(_writeIndented ? " , " : ", ");
            }
        }

        public void AppendAnd(int counter)
        {
            if (_writeIndented && _firstWord)
            {
                _rawSqlBuilder.Append(_indent);
                _formattedSqlBuilder.Append(_indent);
                _firstWord = false;
            }
            if (counter == 0)
            {
                if (_writeIndented)
                {
                    AppendSql("     ");
                }
            }
            else
            {
                AppendSql(" AND ");
            }
        }

        public object GetDefaultVersionNo(object value, Type propertyType)
        {
            if (value == null)
            {
                if (propertyType == typeof(int))
                {
                    return 1;
                }
                if (propertyType == typeof(long))
                {
                    return 1L;
                }

                if (propertyType == typeof(decimal))
                {
                    return 1M;
                }
            }else if (value is int intValue)
            {
                if (intValue <= 0)
                {
                    return 1;
                }
            }else if (value is long longValue)
            {
                if (longValue <= 0L)
                {
                    return 1L;
                }
            }else if (value is decimal decimalValue)
            {
                if (decimalValue <= 0M)
                {
                    return 1M;
                }
            }

            // TODO:
            throw new InvalidOperationException("");
        }

        public void AppendVersion<T>(QueryBuilderParameter<T> parameter, PropertyInfo property)
        {
            var versionAttr = property.GetCustomAttribute<VersionAttribute>();
            if (versionAttr == null) return;
            if (!parameter.IgnoreVersion)
            {
                AppendSql(" + 1 ");
            }
            else
            {
                AppendSql(" ");
            }

            parameter.VersionPropertyInfo = property;
        }

        public void AppendVersion<T>(QueryBuilderParameter<T> parameter, EntityColumnInfo columnInfo)
        {
            if (!columnInfo.IsVersion) return;
            if (!parameter.IgnoreVersion)
            {
                AppendSql(" + 1 ");
            }
            else
            {
                AppendSql(" ");

            }
            parameter.VersionPropertyInfo = columnInfo.PropertyInfo;

        }

        public bool TryAppendIdentity<T>(QueryBuilderParameter<T> parameter, 
            PropertyInfo property,
            string quotedColumnName,
            int counter,
            out object identityValue)
        {
            if (!parameter.Config.UseSqlite())
            {
                identityValue = null;
                return false;
            }
            identityValue = property.GetValue(parameter.Entity);
            parameter.WriteLog($"indentityValue\t{identityValue}");
            if (identityValue == null) return false;
            if (identityValue is int intValue)
            {
                if (intValue <= 0) return false;
            }else if (identityValue is long longValue)
            {
                if (longValue <= 0L) return false;
            }else if (identityValue is decimal decimalValue)
            {
                if (decimalValue <= 0M) return false;
            }

            AppendComma(counter);
            AppendLine(quotedColumnName);
            return true;
        }

        public bool TryAppendIdentityValue<T>(QueryBuilderParameter<T> parameter,
            PropertyInfo property,
            int counter,
            object identityValue)
        {
            if (!parameter.Config.UseSqlite())
            {
                return false;
            }

            if (identityValue == null)
            {
                return false;
            }
            AppendComma(counter);
            AppendParameter(parameter.Config.GetParameterName(property.Name), identityValue);
            AppendLine();
            return true;
        }

        internal bool TryAppendIdentity<T>(QueryBuilderParameter<T> parameter, 
            EntityTypeInfo entityInfo,
            int counter,
            out object identityValue)
        {
            if (!parameter.Config.UseSqlite() || entityInfo.IdentityColumn == null)
            {
                identityValue = null;
                return false;
            }

            var property = entityInfo.IdentityColumn.PropertyInfo;
            identityValue = property.GetValue(parameter.Entity);
            if (identityValue == null) return false;
            if (identityValue is int intValue)
            {
                if (intValue <= 0) return false;
            }
            else if (identityValue is long longValue)
            {
                if (longValue <= 0L) return false;
            }
            else if (identityValue is decimal decimalValue)
            {
                if (decimalValue <= 0M) return false;
            }

            AppendComma(counter);
            AppendLine(parameter.Config.GetQuotedName(entityInfo.IdentityColumn.ColumnName));
            return true;
        }

        internal bool TryAppendIdentityValue<T>(QueryBuilderParameter<T> parameter,
            EntityTypeInfo entityInfo,
            int counter,
            object identityValue)
        {
            if (!parameter.Config.UseSqlite())
            {
                return false;
            }

            if (identityValue == null)
            {
                return false;
            }

            if (entityInfo.IdentityColumn == null)
            {
                return false;
            }

            AppendComma(counter);
            AppendParameter(parameter.Config.GetParameterName(entityInfo.IdentityColumn.PropertyInfo.Name), identityValue);
            AppendLine();
            return true;

        }

        public QueryBuilderResult GetResult()
        {
            return new QueryBuilderResult
                   {
                       ParsedSql = _rawSqlBuilder.ToString(),
                       DebugSql = _formattedSqlBuilder.ToString(),
                       DbDataParameters = _sqlParameters.Values.ToList()
                   };
        }
    }
}
