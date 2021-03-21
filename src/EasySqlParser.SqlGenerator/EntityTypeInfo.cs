﻿using System.Collections.Generic;

namespace EasySqlParser.SqlGenerator
{
    public class EntityTypeInfo
    {
        public string SchemaName { get; set; }

        public string TableName { get; set; }


        public IReadOnlyList<EntityColumnInfo> Columns { get; set; }

        public IReadOnlyList<EntityColumnInfo> KeyColumns { get; set; }

        public EntityColumnInfo IdentityColumn { get; set; }

        public EntityColumnInfo VersionColumn { get; set; }

        public IReadOnlyList<EntityColumnInfo> SequenceColumns { get; set; }

        public bool HasSoftDeleteKey { get; set; }
    }
}
