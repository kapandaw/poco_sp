using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Db.DbObject;
using Db.Helpers;

namespace Db.POCOIterator
{
    public class DbIterator : IPOCOIterator
    {
        #region Constructor

        protected IEnumerable<IDbObjectTraverse> dbObjects;
        protected IPOCOWriter pocoWriter;

        public const string TAB = "    ";
        public virtual string Tab { get; set; }

        public DbIterator(IEnumerable<IDbObjectTraverse> dbObjects, IPOCOWriter pocoWriter)
        {
            this.dbObjects = dbObjects;
            this.pocoWriter = pocoWriter;
            this.Tab = TAB;
        }

        #endregion

        #region Iterate

        public void Iterate()
        {
            Clear();

            if (dbObjects == null || dbObjects.Count() == 0)
                return;

            bool isExistDbObject = (dbObjects.Any(o => o.Error == null));

            string namespaceOffset = string.Empty;
            if (isExistDbObject && !IsSP)
            {
                // Using
                WriteUsing();

                // Namespace Start
                namespaceOffset = WriteNamespaceStart();
            }

            /*IEnumerable<Table> tables = null;
            if (IsNavigationProperties)
            {
                tables = dbObjects.Where(t => t.DbType == DbType.Table).Cast<Table>();
            }*/

            IDbObjectTraverse lastDbObject = dbObjects.Last();
            foreach (IDbObjectTraverse dbObject in dbObjects)
            {
                // Class Name
                string className = GetClassName(dbObject.Database.ToString(), dbObject.Schema, dbObject.Name, dbObject.DbType);
                dbObject.ClassName = className;

                if (dbObject.Error != null)
                {
                    // Error
                    WriteError(dbObject, namespaceOffset);
                }
                else if (!IsSP)
                {
                    // Navigation Properties
                    List<NavigationProperty> navigationProperties = GetNavigationProperties(dbObject/*, tables*/);

                    if (IsWriteObject(navigationProperties, dbObject))
                    {
                        // Class Attributes
                        WriteClassAttributes(dbObject, namespaceOffset);

                        // Class Start
                        WriteClassStart(className, dbObject, namespaceOffset);

                        // Constructor
                        WriteConstructor(className, navigationProperties, dbObject, namespaceOffset);

                        // Columns
                        if (dbObject.Columns != null && dbObject.Columns.Any())
                        {
                            var columns = dbObject.Columns.OrderBy<IColumn, int>(c => c.ColumnOrdinal ?? 0);
                            var lastColumn = columns.Last();
                            foreach (IColumn column in columns)
                                WriteColumn(column, column == lastColumn, dbObject, namespaceOffset);
                        }

                        // Navigation Properties
                        WriteNavigationProperties(navigationProperties, dbObject, namespaceOffset);

                        // Class End
                        WriteClassEnd(dbObject, namespaceOffset);
                    }
                }
                else if (dbObject.DbType == DbType.Table)
                {
                    pocoWriter.WriteComment("-- =============================================");
                    pocoWriter.WriteLine("");
                    pocoWriter.WriteComment("--Author:\t\t<Ovchinnikov Nikolai>");
                    pocoWriter.WriteLine("");
                    pocoWriter.WriteComment("--Create date:\t<01.05.2016>");
                    pocoWriter.WriteLine("");
                    if (Prefix.ToLower().Contains("insert"))
                        pocoWriter.WriteComment(string.Format("--Description:\t<Создание нового [{0}] через EntityFramework>", dbObject.Name));
                    else if (Prefix.ToLower().Contains("update"))
                        pocoWriter.WriteComment(string.Format("--Description:\t<Обновление [{0}] через EntityFramework>", dbObject.Name));
                    else if (Prefix.ToLower().Contains("delete"))
                        pocoWriter.WriteComment(string.Format("--Description:\t<Удаление [{0}] через EntityFramework>", dbObject.Name));

                    pocoWriter.WriteLine("");
                    pocoWriter.WriteComment("-- =============================================");
                    pocoWriter.WriteLine("");

                    pocoWriter.WriteKeyword("CREATE PROC");
                    pocoWriter.Write(" ");
                    pocoWriter.WriteLine(string.Format("[{0}].[{1}{2}]",
                        IsIgnoreDboSchema ? "EF" : dbObject.Schema,
                        IsSingular ? NameHelper.GetSingularName(dbObject.Name) : dbObject.Name,
                        Prefix));

                    // Columns
                    if (dbObject.Columns != null && dbObject.Columns.Any())
                    {
                        var columns = dbObject.Columns.OrderBy<IColumn, int>(c => c.ColumnOrdinal ?? 0);
                        var lastColumn = columns.Last();
                        foreach (IColumn column in columns)
                        {
                            string columnNameModified = column.ColumnName;
                            if (column.DataTypeName.Contains("timestamp"))
                                columnNameModified += "_Original";
                            var precision = String.Empty;
                            if (column.DataTypeName.Contains("char"))
                                precision = string.Format("({0})", column.StringPrecision);
                            if (column.DataTypeName.Contains("decimal"))
                                precision = string.Format("({0},{1})", column.NumericPrecision, column.NumericScale);
                            if (column.DataTypeName.Contains("numeric"))
                                precision = string.Format("({0},{1})", column.NumericPrecision, column.NumericScale);

                            if (Prefix.ToLower().Contains("delete") && (column.IsPrimaryKey || column.DataTypeName == "timestamp"))
                                WriteSpParameter(lastColumn, column, columnNameModified, precision, "");
                            else if (!Prefix.ToLower().Contains("delete"))
                                WriteSpParameter(lastColumn, column, columnNameModified, precision);
                        }
                        pocoWriter.WriteLine("");
                        pocoWriter.WriteLineKeyword("AS BEGIN");
                        pocoWriter.WriteLine("");
                        pocoWriter.Write(Tab);
                        pocoWriter.WriteLineKeyword("SET NOCOUNT ON;");
                        pocoWriter.WriteLine("");

                        if (Prefix.ToLower().Contains("insert"))
                            WriteInsertStoredProcedure(dbObject, columns, lastColumn);
                        else if (Prefix.ToLower().Contains("update"))
                            WriteUpdateStoredProcedure(dbObject, columns, lastColumn);
                        else if (Prefix.ToLower().Contains("delete"))
                            WriteDeleteStoredProcedure(dbObject, columns, lastColumn);

                        pocoWriter.WriteLine("");

                        pocoWriter.WriteLineKeyword("END;");
                        pocoWriter.WriteLineKeyword("GO");
                    }

                    if (!IsSP)
                        WriteClassEnd(dbObject, namespaceOffset);

                }

                if (dbObject != lastDbObject)
                    pocoWriter.WriteLine();
            }

            if (isExistDbObject)
            {
                if (!IsSP)
                    // Namespace End
                    WriteNamespaceEnd();
            }
        }

        private void WriteSpParameter(IColumn lastColumn, IColumn column, string columnNameModified, string precision, string output = " OUTPUT")
        {
            pocoWriter.Write(Tab);
            pocoWriter.Write(string.Format("@{0}", columnNameModified));


            this.pocoWriter.WriteLineKeyword(string.Format(" {0}{1}{2}{3}{4}",
                        column.DataTypeName,
                        precision,
                        chooseNullableParameter(column),
                        output,
                        (lastColumn == column ? "" : ",")));
        }

        private string chooseNullableParameter(IColumn column)
        {
            var isInsert = Prefix.ToLower().Contains("insert");
            var isUpdate = Prefix.ToLower().Contains("update");
            var isDelete = Prefix.ToLower().Contains("delete");

            var isIdentity = false;
            var isState = false;
            if (column.IsIdentity.HasValue)
                isIdentity = column.IsIdentity.Value;

            var isTimestamp = column.DataTypeName == "timestamp";
            var isNullable = column.IsNullable;
            var hasDefault = false;
            if (column.ColumnName.Contains("CreatedDate"))
                hasDefault = true;
            if (column.ColumnName.Contains("CreatedUser"))
                hasDefault = true;
            if (column.ColumnName.Contains("ModifiedDate"))
                hasDefault = true;
            if (column.ColumnName.Contains("ModifiedUser"))
                hasDefault = true;

            if (column.ColumnName.Contains("State"))
                isState = true;

            if (isInsert)
                if (isNullable || hasDefault || isIdentity || isTimestamp || isState)
                    return "=NULL";

            if (isUpdate)
                if (isNullable || hasDefault)
                    if (!isTimestamp)
                        return "=NULL";

            return string.Empty;
        }

        private void WriteInsertStoredProcedure(IDbObjectTraverse dbObject, IOrderedEnumerable<IColumn> columns, IColumn lastColumn)
        {
            var columns2 = columns.Where(column =>
                       !(column.IsIdentity.HasValue ? column.IsIdentity.Value : false)
                           && !column.DataTypeName.Contains("timestamp")
                           && !column.ColumnName.Contains("CreatedDate")
                           && !column.ColumnName.Contains("State")
                           && !column.ColumnName.Contains("ModifiedDate")
               ).ToList();

            if (columns2.Any())
            {
                lastColumn = columns2.Last();
                pocoWriter.Write(Tab);
                pocoWriter.WriteKeyword("INSERT INTO ");
                pocoWriter.WriteLine(GetTableName(dbObject.Schema, dbObject.Name));
                pocoWriter.Write(Tab);
                pocoWriter.Write(Tab);
                pocoWriter.Write("(");

                foreach (IColumn column in columns2)
                {
                    pocoWriter.Write(string.Format("[{0}]{1}", column.ColumnName, (lastColumn != column ? ", " : "")));
                }
                pocoWriter.WriteLine(")");
                pocoWriter.Write(Tab);
                pocoWriter.WriteLineKeyword("VALUES");
                pocoWriter.Write(Tab);
                pocoWriter.Write(Tab);
                pocoWriter.Write("(");
                foreach (IColumn column in columns2)
                {
                    string columnNameModified = column.ColumnName;
                    if (column.DataTypeName.Contains("timestamp"))
                        columnNameModified = string.Format("@{0}_Original", columnNameModified);
                    else
                        columnNameModified = string.Format("@{0}", columnNameModified);
                    pocoWriter.Write(string.Format("{0}{1}", columnNameModified, (lastColumn != column ? ", " : "")));
                }
                pocoWriter.WriteLine(")");
                pocoWriter.WriteLine("");
                pocoWriter.Write(Tab);
                var hasTriiger = false;
                foreach (var name in "Transactor,Agreement,Obligation,Sanction,Document,File,Collateral,Assesment,Checkup,CollateralProject,CreditProject,CreditProjectChange,CourierPackage,AnalisysPackage,Register,RequestPd,ObligationEvent,ObligationEventAction,RequestLgd".Split(','))
                    if (dbObject.Name == name)
                        hasTriiger = true;
                if (hasTriiger)
                    pocoWriter.WriteLineKeyword("SET @Id = @@IDENTITY");
                else
                    pocoWriter.WriteLineKeyword("SET @Id = SCOPE_IDENTITY()");
                pocoWriter.WriteLine("");
                pocoWriter.Write(Tab);
                pocoWriter.WriteKeyword("SELECT ");
                pocoWriter.Write("@Id AS [Id], ");
                WriteSelectAfterInsert(columns);
                pocoWriter.WriteLine("");
                pocoWriter.Write(Tab);
                pocoWriter.WriteKeyword("FROM ");
                pocoWriter.WriteLine(GetTableName(dbObject.Schema, dbObject.Name));
                pocoWriter.Write(Tab);
                pocoWriter.WriteKeyword("WHERE ");
                pocoWriter.Write("Id = @Id");
                pocoWriter.WriteLineKeyword(" AND @@ROWCOUNT > 0 ");
                pocoWriter.WriteLine("");
            }
        }

        private void WriteSelectAfterInsert(IOrderedEnumerable<IColumn> columns)
        {
            var selectColumns = columns.Where(column => !(column.IsIdentity.HasValue ? column.IsIdentity.Value : false)).ToList();
            if (selectColumns.Any())
            {
                var lastColumn = selectColumns.Last();
                foreach (var column in selectColumns)
                    pocoWriter.Write(string.Format("[{0}]{1} ", column.ColumnName, column != lastColumn ? "," : ""));
            }
        }

        private void WriteUpdateStoredProcedure(IDbObjectTraverse dbObject, IOrderedEnumerable<IColumn> columns, IColumn lastColumn)
        {
            var columns2 = columns.Where(column =>
                    !column.IsPrimaryKey
                        && !(column.IsIdentity.HasValue ? column.IsIdentity.Value : false)
                            && !column.DataTypeName.Contains("timestamp")
                            && !column.ColumnName.Contains("CreatedDate")
                            && !column.ColumnName.Contains("CreatedUser")
                );
            if (columns2.Any())
            {
                lastColumn = columns2.Last();
                pocoWriter.Write(Tab);
                pocoWriter.WriteKeyword("UPDATE ");
                pocoWriter.WriteLine(GetTableName(dbObject.Schema, dbObject.Name));
                pocoWriter.Write(Tab);
                pocoWriter.Write(Tab);
                pocoWriter.WriteLineKeyword("SET");
                foreach (IColumn column in columns2)
                {
                    pocoWriter.Write(Tab);
                    pocoWriter.Write(Tab);
                    pocoWriter.Write(Tab);

                    if (column.ColumnName.Contains("State"))
                        pocoWriter.WriteLine(string.Format("[{0}] = ISNULL(@{1}, [{0}]){2}", column.ColumnName, column.ColumnName, (lastColumn != column ? ", " : "")));
                    else if (column.ColumnName.Contains("ModifiedDate"))
                        pocoWriter.WriteLine(string.Format("[{0}] = {1}{2}", column.ColumnName, "GETDATE()", (lastColumn != column ? ", " : "")));
                    else
                        pocoWriter.WriteLine(string.Format("[{0}] = @{1}{2}", column.ColumnName, column.ColumnName, (lastColumn != column ? ", " : "")));
                }
                pocoWriter.WriteLine("");
                pocoWriter.Write(Tab);
                pocoWriter.Write(Tab);
                pocoWriter.WriteKeyword("WHERE ");
                WriteEFWhereSP(columns, lastColumn);
            }
            pocoWriter.WriteLine("");
            pocoWriter.WriteLine("");
            pocoWriter.Write(Tab);
            pocoWriter.WriteKeyword("SELECT ");
            WriteSelectAfterInsert(columns);
            pocoWriter.WriteLine("");
            pocoWriter.Write(Tab);
            pocoWriter.WriteKeyword("FROM ");
            pocoWriter.WriteLine(GetTableName(dbObject.Schema, dbObject.Name));
            pocoWriter.Write(Tab);
            pocoWriter.WriteKeyword("WHERE ");
            pocoWriter.WriteLine("Id = @Id AND @@ROWCOUNT>0");

            pocoWriter.WriteLine("");


        }

        private void WriteDeleteStoredProcedure(IDbObjectTraverse dbObject, IOrderedEnumerable<IColumn> columns, IColumn lastColumn)
        {
            pocoWriter.Write(Tab);
            pocoWriter.WriteKeyword("DELETE FROM ");
            pocoWriter.WriteLine(GetTableName(dbObject.Schema, dbObject.Name));
            pocoWriter.Write(Tab);
            pocoWriter.WriteKeyword("WHERE ");
            WriteEFWhereSP(columns, lastColumn);
            pocoWriter.WriteLine("");
            //pocoWriter.WriteKeyword("SELECT SCOPE_IDENTITY() AS");
            //pocoWriter.WriteLine(" Id");
        }

        private void WriteEFWhereSP(IOrderedEnumerable<IColumn> columns, IColumn lastColumn)
        {
            var result = string.Empty;
            foreach (IColumn column in columns.Where(c => c.IsPrimaryKey || c.DataTypeName.Contains("timestamp")))
            {
                string columnNameModified = column.ColumnName;
                if (column.DataTypeName.Contains("timestamp"))
                    columnNameModified += "_Original";
                result += string.Format("[{0}] = @{1}{2}", column.ColumnName, columnNameModified, (lastColumn != column ? " AND " : ""));

            }
            var correct = result.EndsWith(" AND ");
            if (correct)
                result = result.Substring(0, result.Length - 4);

            pocoWriter.Write(result);
        }

        #endregion

        #region Clear

        public void Clear()
        {
            pocoWriter.Clear();
        }

        #endregion

        #region Using

        public virtual bool IsUsing { get; set; }

        protected virtual void WriteUsing()
        {
            if (IsUsing)
            {
                WriteUsingClause();
                pocoWriter.WriteLine();
            }
        }

        protected virtual void WriteUsingClause()
        {
            if (!IsSP)
            {
                pocoWriter.WriteKeyword("using");
                pocoWriter.WriteLine(" System;");

                if (IsNavigationProperties)
                {
                    pocoWriter.WriteKeyword("using");
                    pocoWriter.WriteLine(" System.Collections.Generic;");
                }

                if (IsSpecialSQLTypes())
                {
                    pocoWriter.WriteKeyword("using");
                    pocoWriter.WriteLine(" Microsoft.SqlServer.Types;");
                }

                if (IsDataContract)
                {
                    pocoWriter.WriteKeyword("using");
                    pocoWriter.WriteLine(" System.Runtime.Serialization;");
                }
            }
        }

        protected virtual bool IsSpecialSQLTypes()
        {
            if (dbObjects == null || dbObjects.Count() == 0)
                return false;

            foreach (var dbObject in dbObjects)
            {
                if (dbObject.Columns != null && dbObject.Columns.Any())
                {
                    foreach (IColumn column in dbObject.Columns)
                    {
                        string data_type = (column.DataTypeName ?? string.Empty).ToLower();
                        if (data_type.Contains("geography") || data_type.Contains("geometry") || data_type.Contains("hierarchyid"))
                            return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region Namespace Start

        public virtual string Namespace { get; set; }

        protected virtual string WriteNamespaceStart()
        {
            string namespaceOffset = string.Empty;

            if (!IsSP)
            {
                if (string.IsNullOrEmpty(Namespace) == false)
                {
                    WriteNamespaceStartClause();
                    namespaceOffset = Tab;
                }
            }

            return namespaceOffset;
        }

        protected virtual void WriteNamespaceStartClause()
        {
            if (!IsSP)
            {
                pocoWriter.WriteKeyword("namespace");
                pocoWriter.Write(" ");
                pocoWriter.WriteLine(Namespace);
                pocoWriter.WriteLine("{");
            }
        }

        #endregion

        #region Error

        protected virtual void WriteError(IDbObjectTraverse dbObject, string namespaceOffset)
        {
            pocoWriter.Write(namespaceOffset);
            pocoWriter.WriteLineError("/*");

            pocoWriter.Write(namespaceOffset);
            pocoWriter.WriteLineError(string.Format("{0}.{1}", dbObject.Database.ToString(), dbObject.ToString()));

            Exception currentError = dbObject.Error;
            while (currentError != null)
            {
                pocoWriter.Write(namespaceOffset);
                pocoWriter.WriteLineError(currentError.Message);
                currentError = currentError.InnerException;
            }

            pocoWriter.Write(namespaceOffset);
            pocoWriter.WriteLineError("*/");
        }

        #endregion

        #region Is Write Object

        protected virtual bool IsWriteObject(List<NavigationProperty> navigationProperties, IDbObjectTraverse dbObject)
        {
            if (dbObject.DbType == DbType.Table)
            {
                if (IsNavigationPropertiesShowJoinTable == false)
                {
                    if (navigationProperties != null && navigationProperties.Count > 0)
                    {
                        // hide many-to-many join table.
                        // join table is complete. all the columns are part of the pk. there are no columns other than the pk.
                        return navigationProperties.All(p => p.IsRefFrom && p.ForeignKey.Is_Many_To_Many && p.ForeignKey.Is_Many_To_Many_Complete) == false;
                    }
                }
            }

            return true;
        }

        #endregion

        #region Class Attributes

        protected virtual void WriteClassAttributes(IDbObjectTraverse dbObject, string namespaceOffset)
        {
            if (IsDataContract)
            {
                if (IsDataContract)
                {
                    pocoWriter.Write(namespaceOffset);
                    pocoWriter.Write("[");
                    pocoWriter.WriteUserType("DataContract(Namespace = \"http://schemas.fuib.com/rmc/contracts/\")");
                    pocoWriter.WriteLine("]");
                }
            }
        }

        #endregion

        #region Class Name

        public virtual string Prefix { get; set; }
        public virtual string FixedClassName { get; set; }
        public virtual bool IsIncludeDB { get; set; }
        public virtual bool IsCamelCase { get; set; }
        public virtual string WordsSeparator { get; set; }
        public virtual bool IsUpperCase { get; set; }
        public virtual bool IsLowerCase { get; set; }
        public virtual string DBSeparator { get; set; }
        public virtual bool IsIncludeSchema { get; set; }
        public virtual bool IsIgnoreDboSchema { get; set; }
        public virtual string SchemaSeparator { get; set; }
        public virtual bool IsSingular { get; set; }
        public virtual string Search { get; set; }
        public virtual string Replace { get; set; }
        public virtual bool IsSearchIgnoreCase { get; set; }
        public virtual string Suffix { get; set; }

        public virtual bool IsSP { get; set; }
        public virtual bool IsDataContract { get; set; }

        protected virtual string GetClassName(string database, string schema, string name, DbType dbType)
        {
            string className = null;

            // prefix
            if (string.IsNullOrEmpty(Prefix) == false)
                className += Prefix;

            if (string.IsNullOrEmpty(FixedClassName))
            {
                if (IsIncludeDB)
                {
                    // database
                    if (IsCamelCase || string.IsNullOrEmpty(WordsSeparator) == false)
                        className += NameHelper.TransformName(database, WordsSeparator, IsCamelCase, IsUpperCase, IsLowerCase);
                    else if (IsUpperCase)
                        className += database.ToUpper();
                    else if (IsLowerCase)
                        className += database.ToLower();
                    else
                        className += database;

                    // db separator
                    if (string.IsNullOrEmpty(DBSeparator) == false)
                        className += DBSeparator;
                }

                if (IsIncludeSchema)
                {
                    if (IsIgnoreDboSchema == false || schema != "dbo")
                    {
                        // schema
                        if (IsCamelCase || string.IsNullOrEmpty(WordsSeparator) == false)
                            className += NameHelper.TransformName(schema, WordsSeparator, IsCamelCase, IsUpperCase, IsLowerCase);
                        else if (IsUpperCase)
                            className += schema.ToUpper();
                        else if (IsLowerCase)
                            className += schema.ToLower();
                        else
                            className += schema;

                        // schema separator
                        if (string.IsNullOrEmpty(SchemaSeparator) == false)
                            className += SchemaSeparator;
                    }
                }

                // name
                if (IsSingular)
                {
                    if (dbType == DbType.Table || dbType == DbType.View || dbType == DbType.TVP)
                        name = NameHelper.GetSingularName(name);
                }

                if (IsCamelCase || string.IsNullOrEmpty(WordsSeparator) == false)
                    className += NameHelper.TransformName(name, WordsSeparator, IsCamelCase, IsUpperCase, IsLowerCase);
                else if (IsUpperCase)
                    className += name.ToUpper();
                else if (IsLowerCase)
                    className += name.ToLower();
                else
                    className += name;

                if (string.IsNullOrEmpty(Search) == false)
                {
                    if (IsSearchIgnoreCase)
                        className = Regex.Replace(className, Search, Replace ?? string.Empty, RegexOptions.IgnoreCase);
                    else
                        className = className.Replace(Search, Replace ?? string.Empty);
                }
            }
            else
            {
                // fixed name
                className += FixedClassName;
            }

            // postfix
            if (string.IsNullOrEmpty(Suffix) == false)
                className += Suffix;

            return className;
        }

        protected virtual string GetSPName(string schema, string name, string sufix)
        {
            string spName = string.Empty;

            if (string.IsNullOrWhiteSpace(schema))
                schema = "dbo";
            if (string.IsNullOrWhiteSpace(name))
                name = "???";
            if (string.IsNullOrWhiteSpace(sufix))
                sufix = string.Empty;
            if (string.IsNullOrWhiteSpace(Prefix))
                Prefix = string.Empty;

            // name
            if (IsSingular)
            {
                name = NameHelper.GetSingularName(name);
            }

            spName = string.Format("[{0}].[{1}{2}]", schema, spName, Prefix);
            return spName;
        }

        protected virtual string GetTableName(string schema, string name)
        {
            string tableName = string.Empty;

            if (string.IsNullOrWhiteSpace(schema))
                schema = "dbo";
            if (string.IsNullOrWhiteSpace(name))
                name = "???";

            tableName = string.Format("[{0}].[{1}]", schema, name);
            return tableName;
        }

        protected virtual string GetSPParameterName(string name)
        {
            string paramName = string.Empty;

            if (string.IsNullOrWhiteSpace(name))
                name = "???";
            paramName = string.Format("@{0}", name);
            return paramName;
        }

        protected virtual string GetSPParameterNameForDeclare(string name, string type, bool isOutput)
        {
            string paramName = string.Empty;

            if (string.IsNullOrWhiteSpace(type))
                type = "sqlvariant";
            if (string.IsNullOrWhiteSpace(name))
                name = "???";
            if (isOutput)
                type = string.Format("{0}\t\t{1}", type, "OUTPUT");
            paramName = string.Format("{0}\t\t\t{1}", GetSPParameterName(name), type);
            return paramName;
        }

        #endregion

        #region Class Start

        public virtual bool IsPartialClass { get; set; }
        public virtual string Inherit { get; set; }

        protected virtual void WriteClassStart(string className, IDbObjectTraverse dbObject, string namespaceOffset)
        {
            pocoWriter.Write(namespaceOffset);
            pocoWriter.WriteKeyword("public");
            pocoWriter.Write(" ");
            if (IsPartialClass)
            {
                pocoWriter.WriteKeyword("partial");
                pocoWriter.Write(" ");
            }
            pocoWriter.WriteKeyword("class");
            pocoWriter.Write(" ");
            pocoWriter.WriteUserType(className);
            if (string.IsNullOrEmpty(Inherit) == false)
            {
                pocoWriter.Write(" : ");
                string[] inherit = Inherit.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                pocoWriter.WriteUserType(inherit[0]);
                for (int i = 1; i < inherit.Length; i++)
                {
                    pocoWriter.Write(", ");
                    pocoWriter.WriteUserType(inherit[i]);
                }
            }
            pocoWriter.WriteLine();

            pocoWriter.Write(namespaceOffset);
            pocoWriter.WriteKeyword("{");
            pocoWriter.WriteLine("");
        }

        #endregion

        #region Class Constructor

        protected virtual void WriteConstructor(string className, List<NavigationProperty> navigationProperties, IDbObjectTraverse dbObject, string namespaceOffset)
        {
            if (!IsSP)
            {
                if (IsNavigableObject(dbObject))
                {
                    bool isWriteConstructor =
                        navigationProperties != null &&
                        navigationProperties.Count > 0 &&
                        navigationProperties.Any(p => p.IsSingle == false);

                    if (isWriteConstructor)
                    {
                        WriteConstructorStart(className, dbObject, namespaceOffset);
                        WriteConstructorContent(navigationProperties, dbObject, namespaceOffset);
                        WriteConstructorEnd(dbObject, namespaceOffset);
                        pocoWriter.WriteLine();
                    }
                }
            }
        }

        protected virtual void WriteConstructorStart(string className, IDbObjectTraverse dbObject, string namespaceOffset)
        {
            pocoWriter.Write(namespaceOffset);
            pocoWriter.Write(Tab);
            pocoWriter.WriteKeyword("public");
            pocoWriter.Write(" ");
            pocoWriter.Write(className);
            pocoWriter.WriteLine("()");

            pocoWriter.Write(namespaceOffset);
            pocoWriter.Write(Tab);
            pocoWriter.WriteLine("{");
        }

        protected virtual void WriteConstructorContent(List<NavigationProperty> navigationProperties, IDbObjectTraverse dbObject, string namespaceOffset)
        {
            foreach (var np in navigationProperties.Where(p => p.IsSingle == false))
                WriteNavigationPropertyConstructorInitialization(np, namespaceOffset);
        }

        protected virtual void WriteNavigationPropertyConstructorInitialization(NavigationProperty navigationProperty, string namespaceOffset)
        {
            pocoWriter.Write(namespaceOffset);
            pocoWriter.Write(Tab);
            pocoWriter.Write(Tab);
            pocoWriter.WriteKeyword("this");
            pocoWriter.Write(".");
            pocoWriter.Write(navigationProperty.ToString());
            pocoWriter.Write(" = ");
            pocoWriter.WriteKeyword("new");
            pocoWriter.Write(" ");
            pocoWriter.WriteUserType(IsNavigationPropertiesICollection ? "HashSet" : "List");
            pocoWriter.Write("<");
            pocoWriter.WriteUserType(navigationProperty.ClassName);
            pocoWriter.WriteLine(">();");
        }

        protected virtual void WriteConstructorEnd(IDbObjectTraverse dbObject, string namespaceOffset)
        {
            pocoWriter.Write(namespaceOffset);
            pocoWriter.Write(Tab);
            pocoWriter.WriteLine("}");
        }

        #endregion

        #region Column Attributes

        protected virtual void WriteColumnAttributes(IColumn column, string cleanColumnName, IDbObjectTraverse dbObject, string namespaceOffset)
        {
            if (IsDataContract)
            {
                pocoWriter.Write(namespaceOffset);
                pocoWriter.Write(Tab);
                pocoWriter.Write("[");
                pocoWriter.WriteUserType("DataMember");
                pocoWriter.WriteLine("]");
            }
        }

        #endregion

        #region Column

        public virtual bool IsProperties { get; set; }
        public virtual bool IsVirtualProperties { get; set; }
        public virtual bool IsAllStructNullable { get; set; }
        public virtual bool IsComments { get; set; }
        public virtual bool IsCommentsWithoutNull { get; set; }
        public virtual bool IsNewLineBetweenMembers { get; set; }

        protected virtual void WriteColumn(IColumn column, bool isLastColumn, IDbObjectTraverse dbObject, string namespaceOffset)
        {
            string cleanColumnName = NameHelper.CleanName(column.ColumnName);


            WriteColumnAttributes(column, cleanColumnName, dbObject, namespaceOffset);

            WriteColumnStart(namespaceOffset);

            WriteColumnDataType(column);

            WriteColumnName(cleanColumnName);

            WriteColumnEnd();

            WriteColumnComments(column);

            pocoWriter.WriteLine();

            if (IsNewLineBetweenMembers && isLastColumn == false)
                pocoWriter.WriteLine();
        }

        protected virtual void WriteColumnStart(string namespaceOffset)
        {
            pocoWriter.Write(namespaceOffset);
            pocoWriter.Write(Tab);
            pocoWriter.WriteKeyword("public");
            pocoWriter.Write(" ");

            if (IsProperties && IsVirtualProperties)
            {
                pocoWriter.WriteKeyword("virtual");
                pocoWriter.Write(" ");
            }
        }

        protected virtual void WriteColumnDataType(IColumn column)
        {
            switch ((column.DataTypeDisplay ?? string.Empty).ToLower())
            {
                case "bigint": WriteColumnBigInt(column.IsNullable); break;
                case "binary": WriteColumnBinary(); break;
                case "bit": WriteColumnBit(column.IsNullable); break;
                case "char": WriteColumnChar(); break;
                case "date": WriteColumnDate(column.IsNullable); break;
                case "datetime": WriteColumnDateTime(column.IsNullable); break;
                case "datetime2": WriteColumnDateTime2(column.IsNullable); break;
                case "datetimeoffset": WriteColumnDateTimeOffset(column.IsNullable); break;
                case "decimal": WriteColumnDecimal(column.IsNullable); break;
                case "filestream": WriteColumnFileStream(); break;
                case "float": WriteColumnFloat(column.IsNullable); break;
                case "geography": WriteColumnGeography(); break;
                case "geometry": WriteColumnGeometry(); break;
                case "hierarchyid": WriteColumnHierarchyId(); break;
                case "image": WriteColumnImage(); break;
                case "int": WriteColumnInt(column.IsNullable); break;
                case "money": WriteColumnMoney(column.IsNullable); break;
                case "nchar": WriteColumnNChar(); break;
                case "ntext": WriteColumnNText(); break;
                case "numeric": WriteColumnNumeric(column.IsNullable); break;
                case "nvarchar": WriteColumnNVarChar(); break;
                case "real": WriteColumnReal(column.IsNullable); break;
                case "rowversion": WriteColumnRowVersion(); break;
                case "smalldatetime": WriteColumnSmallDateTime(column.IsNullable); break;
                case "smallint": WriteColumnSmallInt(column.IsNullable); break;
                case "smallmoney": WriteColumnSmallMoney(column.IsNullable); break;
                case "sql_variant": WriteColumnSqlVariant(); break;
                case "text": WriteColumnText(); break;
                case "time": WriteColumnTime(column.IsNullable); break;
                case "timestamp": WriteColumnTimeStamp(); break;
                case "tinyint": WriteColumnTinyInt(column.IsNullable); break;
                case "uniqueidentifier": WriteColumnUniqueIdentifier(column.IsNullable); break;
                case "varbinary": WriteColumnVarBinary(); break;
                case "varchar": WriteColumnVarChar(); break;
                case "xml": WriteColumnXml(); break;
                default: WriteColumnObject(); break;
            }
        }

        protected virtual void WriteColumnName(string columnName)
        {
            pocoWriter.Write(" ");
            pocoWriter.Write(columnName);
        }

        protected virtual void WriteColumnEnd()
        {
            if (IsProperties)
            {
                pocoWriter.Write(" { ");
                pocoWriter.WriteKeyword("get");
                pocoWriter.Write("; ");
                pocoWriter.WriteKeyword("set");
                pocoWriter.Write("; }");
            }
            else
            {
                pocoWriter.Write(";");
            }
        }

        protected virtual void WriteColumnComments(IColumn column)
        {
            if (IsComments)
            {
                pocoWriter.Write(" ");
                pocoWriter.WriteComment("//");
                pocoWriter.WriteComment(" ");
                pocoWriter.WriteComment(column.DataTypeDisplay);
                pocoWriter.WriteComment(column.Precision ?? string.Empty);

                if (IsCommentsWithoutNull == false)
                {
                    pocoWriter.WriteComment(",");
                    pocoWriter.WriteComment(" ");
                    pocoWriter.WriteComment((column.IsNullable ? "null" : "not null"));
                }
            }
        }

        #region Column Data Types

        protected virtual void WriteColumnBigInt(bool isNullable)
        {
            pocoWriter.WriteKeyword("long");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnBinary()
        {
            pocoWriter.WriteKeyword("byte");
            pocoWriter.Write("[]");
        }

        protected virtual void WriteColumnBit(bool isNullable)
        {
            pocoWriter.WriteKeyword("bool");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnChar()
        {
            pocoWriter.WriteKeyword("string");
        }

        protected virtual void WriteColumnDate(bool isNullable)
        {
            pocoWriter.WriteUserType("DateTime");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnDateTime(bool isNullable)
        {
            pocoWriter.WriteUserType("DateTime");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnDateTime2(bool isNullable)
        {
            pocoWriter.WriteUserType("DateTime");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnDateTimeOffset(bool isNullable)
        {
            pocoWriter.WriteUserType("DateTimeOffset");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnDecimal(bool isNullable)
        {
            pocoWriter.WriteKeyword("decimal");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnFileStream()
        {
            pocoWriter.WriteKeyword("byte");
            pocoWriter.Write("[]");
        }

        protected virtual void WriteColumnFloat(bool isNullable)
        {
            pocoWriter.WriteKeyword("double");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnGeography()
        {
            if (IsUsing == false)
                pocoWriter.Write("Microsoft.SqlServer.Types.");
            pocoWriter.WriteUserType("SqlGeography");
        }

        protected virtual void WriteColumnGeometry()
        {
            if (IsUsing == false)
                pocoWriter.Write("Microsoft.SqlServer.Types.");
            pocoWriter.WriteUserType("SqlGeometry");
        }

        protected virtual void WriteColumnHierarchyId()
        {
            if (IsUsing == false)
                pocoWriter.Write("Microsoft.SqlServer.Types.");
            pocoWriter.WriteUserType("SqlHierarchyId");
        }

        protected virtual void WriteColumnImage()
        {
            pocoWriter.WriteKeyword("byte");
            pocoWriter.Write("[]");
        }

        protected virtual void WriteColumnInt(bool isNullable)
        {
            pocoWriter.WriteKeyword("int");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnMoney(bool isNullable)
        {
            pocoWriter.WriteKeyword("decimal");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnNChar()
        {
            pocoWriter.WriteKeyword("string");
        }

        protected virtual void WriteColumnNText()
        {
            pocoWriter.WriteKeyword("string");
        }

        protected virtual void WriteColumnNumeric(bool isNullable)
        {
            pocoWriter.WriteKeyword("decimal");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnNVarChar()
        {
            pocoWriter.WriteKeyword("string");
        }

        protected virtual void WriteColumnReal(bool isNullable)
        {
            pocoWriter.WriteUserType("Single");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnRowVersion()
        {
            pocoWriter.WriteKeyword("byte");
            pocoWriter.Write("[]");
        }

        protected virtual void WriteColumnSmallDateTime(bool isNullable)
        {
            pocoWriter.WriteUserType("DateTime");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnSmallInt(bool isNullable)
        {
            pocoWriter.WriteKeyword("short");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnSmallMoney(bool isNullable)
        {
            pocoWriter.WriteKeyword("decimal");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnSqlVariant()
        {
            pocoWriter.WriteKeyword("object");
        }

        protected virtual void WriteColumnText()
        {
            pocoWriter.WriteKeyword("string");
        }

        protected virtual void WriteColumnTime(bool isNullable)
        {
            pocoWriter.WriteUserType("TimeSpan");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnTimeStamp()
        {
            pocoWriter.WriteKeyword("byte");
            pocoWriter.Write("[]");
        }

        protected virtual void WriteColumnTinyInt(bool isNullable)
        {
            pocoWriter.WriteKeyword("byte");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnUniqueIdentifier(bool isNullable)
        {
            pocoWriter.WriteUserType("Guid");
            if (isNullable || IsAllStructNullable)
                pocoWriter.Write("?");
        }

        protected virtual void WriteColumnVarBinary()
        {
            pocoWriter.WriteKeyword("byte");
            pocoWriter.Write("[]");
        }

        protected virtual void WriteColumnVarChar()
        {
            pocoWriter.WriteKeyword("string");
        }

        protected virtual void WriteColumnXml()
        {
            pocoWriter.WriteKeyword("string");
        }

        protected virtual void WriteColumnObject()
        {
            pocoWriter.WriteKeyword("object");
        }

        #endregion

        #endregion

        #region Navigation Properties

        public virtual bool IsNavigationProperties { get; set; }
        public virtual bool IsNavigationPropertiesVirtual { get; set; }
        public virtual bool IsNavigationPropertiesShowJoinTable { get; set; }
        public virtual bool IsNavigationPropertiesComments { get; set; }
        public virtual bool IsNavigationPropertiesList { get; set; }
        public virtual bool IsNavigationPropertiesICollection { get; set; }
        public virtual bool IsNavigationPropertiesIEnumerable { get; set; }

        protected virtual bool IsNavigableObject(IDbObjectTraverse dbObject)
        {
            return (IsNavigationProperties && dbObject.DbType == DbType.Table);
        }

        #region Get Navigation Properties

        protected virtual List<NavigationProperty> GetNavigationProperties(IDbObjectTraverse dbObject/*, IEnumerable<Table> tables*/)
        {
            List<NavigationProperty> navigationProperties = null;

            if (IsNavigableObject(dbObject))
            {
                if (dbObject.Columns != null && dbObject.Columns.Any())
                {
                    // columns are referencing (IsForeignKey)
                    var columnsFrom = dbObject.Columns.Where(c => c.HasForeignKeys).OrderBy<IColumn, int>(c => c.ColumnOrdinal ?? 0);
                    if (columnsFrom.Any())
                    {
                        if (navigationProperties == null)
                            navigationProperties = new List<NavigationProperty>();

                        foreach (var column in columnsFrom.Cast<TableColumn>())
                        {
                            foreach (var fk in column.ForeignKeys)
                            {
                                string className = GetClassName(dbObject.Database.ToString(), fk.Primary_Schema, fk.Primary_Table, dbObject.DbType);
                                fk.NavigationPropertyRefFrom.ClassName = className;
                                navigationProperties.Add(fk.NavigationPropertyRefFrom);
                            }
                        }
                    }

                    // columns are referenced (IsPrimaryForeignKey)
                    var columnsTo = dbObject.Columns.Where(c => c.HasPrimaryForeignKeys).OrderBy<IColumn, int>(c => c.ColumnOrdinal ?? 0);
                    if (columnsTo.Any())
                    {
                        if (navigationProperties == null)
                            navigationProperties = new List<NavigationProperty>();

                        foreach (var column in columnsTo.Cast<TableColumn>())
                        {
                            foreach (var fk in column.PrimaryForeignKeys)
                            {
                                string className = GetClassName(dbObject.Database.ToString(), fk.Foreign_Schema, fk.Foreign_Table, dbObject.DbType);

                                if (IsNavigationPropertiesShowJoinTable == false && fk.NavigationPropertiesRefToManyToMany != null)
                                {
                                    foreach (var np in fk.NavigationPropertiesRefToManyToMany)
                                    {
                                        np.ClassName = className;
                                        navigationProperties.Add(np);
                                    }
                                }
                                else
                                {
                                    fk.NavigationPropertyRefTo.ClassName = className;
                                    navigationProperties.Add(fk.NavigationPropertyRefTo);
                                }
                            }
                        }
                    }

                    // remove tables that don't participate
                    /*if (navigationProperties != null && navigationProperties.Count > 0)
                    {
                        if (tables == null || tables.Count() == 0)
                        {
                            navigationProperties.Clear();
                        }
                        else
                        {
                            navigationProperties.RemoveAll(np => (
                                (np.IsRefFrom && tables.Contains(np.ForeignKey.ToTable)) ||
                                (np.IsRefFrom == false && tables.Contains(np.ForeignKey.FromTable))
                            ) == false);
                        }
                    }*/

                    // rename duplicates
                    RenameDuplicateNavigationProperties(navigationProperties, dbObject);
                }
            }

            return navigationProperties;
        }

        protected static readonly Regex regexEndNumber = new Regex("(\\d+)$", RegexOptions.Compiled);

        protected virtual void RenameDuplicateNavigationProperties(List<NavigationProperty> navigationProperties, IDbObjectTraverse dbObject)
        {
            if (navigationProperties != null && navigationProperties.Count > 0)
            {
                // groups of navigation properties with the same name
                var groups1 = navigationProperties.GroupBy(p => p.ToString()).Where(g => g.Count() > 1);

                // if the original column name ended with a number, then assign that number to the property name
                foreach (var group in groups1)
                {
                    foreach (var np in group)
                    {
                        string columnName = (np.IsRefFrom ? np.ForeignKey.Primary_Column : np.ForeignKey.Foreign_Column);
                        var match = regexEndNumber.Match(columnName);
                        if (match.Success)
                            np.RenamedPropertyName = np.ToString() + match.Value;
                    }
                }

                // if there are still duplicate property names, then rename them with a running number suffix
                var groups2 = navigationProperties.GroupBy(p => p.ToString()).Where(g => g.Count() > 1);
                foreach (var group in groups2)
                {
                    int suffix = 1;
                    foreach (var np in group.Skip(1))
                        np.RenamedPropertyName = np.ToString() + (suffix++);
                }
            }
        }

        #endregion

        #region Write Navigation Properties

        protected virtual void WriteNavigationProperties(List<NavigationProperty> navigationProperties, IDbObjectTraverse dbObject, string namespaceOffset)
        {
            if (IsNavigableObject(dbObject))
            {
                if (navigationProperties != null && navigationProperties.Count > 0)
                {
                    if (IsNewLineBetweenMembers == false)
                        pocoWriter.WriteLine();

                    foreach (var np in navigationProperties)
                        WriteNavigationProperty(np, dbObject, namespaceOffset);
                }
            }
        }

        protected virtual void WriteNavigationProperty(NavigationProperty navigationProperty, IDbObjectTraverse dbObject, string namespaceOffset)
        {
            if (IsNewLineBetweenMembers)
                pocoWriter.WriteLine();

            WriteNavigationPropertyComments(navigationProperty, dbObject, namespaceOffset);

            WriteNavigationPropertyAttributes(navigationProperty, dbObject, namespaceOffset);

            if (navigationProperty.IsSingle)
                WriteNavigationPropertySingle(navigationProperty, dbObject, namespaceOffset);
            else
                WriteNavigationPropertyMultiple(navigationProperty, dbObject, namespaceOffset);
        }

        protected virtual void WriteNavigationPropertyComments(NavigationProperty navigationProperty, IDbObjectTraverse dbObject, string namespaceOffset)
        {
            if (IsNavigationPropertiesComments)
            {
                pocoWriter.Write(namespaceOffset);
                pocoWriter.Write(Tab);
                pocoWriter.WriteComment("// ");
                pocoWriter.WriteComment(navigationProperty.ForeignKey.Foreign_Schema);
                pocoWriter.WriteComment(".");
                pocoWriter.WriteComment(navigationProperty.ForeignKey.Foreign_Table);
                pocoWriter.WriteComment(".");
                pocoWriter.WriteComment(navigationProperty.ForeignKey.Foreign_Column);
                pocoWriter.WriteComment(" -> ");
                pocoWriter.WriteComment(navigationProperty.ForeignKey.Primary_Schema);
                pocoWriter.WriteComment(".");
                pocoWriter.WriteComment(navigationProperty.ForeignKey.Primary_Table);
                pocoWriter.WriteComment(".");
                pocoWriter.WriteComment(navigationProperty.ForeignKey.Primary_Column);
                if (string.IsNullOrEmpty(navigationProperty.ForeignKey.Name) == false)
                {
                    pocoWriter.WriteComment(" (");
                    pocoWriter.WriteComment(navigationProperty.ForeignKey.Name);
                    pocoWriter.WriteComment(")");
                }
                pocoWriter.WriteLine();
            }
        }

        protected virtual void WriteNavigationPropertyAttributes(NavigationProperty navigationProperty, IDbObjectTraverse dbObject, string namespaceOffset)
        {
        }

        protected virtual void WriteNavigationPropertySingle(NavigationProperty navigationProperty, IDbObjectTraverse dbObject, string namespaceOffset)
        {
            WriteNavigationPropertyStart(namespaceOffset);
            pocoWriter.WriteUserType(navigationProperty.ClassName);
            pocoWriter.Write(" ");
            pocoWriter.Write(navigationProperty.ToString());
            WriteNavigationPropertyEnd();
            pocoWriter.WriteLine();
        }

        protected virtual void WriteNavigationPropertyMultiple(NavigationProperty navigationProperty, IDbObjectTraverse dbObject, string namespaceOffset)
        {
            WriteNavigationPropertyStart(namespaceOffset);
            if (IsNavigationPropertiesList)
                pocoWriter.WriteUserType("List");
            else if (IsNavigationPropertiesICollection)
                pocoWriter.WriteUserType("ICollection");
            else if (IsNavigationPropertiesIEnumerable)
                pocoWriter.WriteUserType("IEnumerable");
            pocoWriter.Write("<");
            pocoWriter.WriteUserType(navigationProperty.ClassName);
            pocoWriter.Write("> ");
            pocoWriter.Write(navigationProperty.ToString());
            WriteNavigationPropertyEnd();
            pocoWriter.WriteLine();
        }

        protected virtual void WriteNavigationPropertyStart(string namespaceOffset)
        {
            pocoWriter.Write(namespaceOffset);
            pocoWriter.Write(Tab);
            pocoWriter.WriteKeyword("public");
            pocoWriter.Write(" ");

            if (IsProperties && IsNavigationPropertiesVirtual)
            {
                pocoWriter.WriteKeyword("virtual");
                pocoWriter.Write(" ");
            }
        }

        protected virtual void WriteNavigationPropertyEnd()
        {
            if (IsProperties)
            {
                pocoWriter.Write(" { ");
                pocoWriter.WriteKeyword("get");
                pocoWriter.Write("; ");
                pocoWriter.WriteKeyword("set");
                pocoWriter.Write("; }");
            }
            else
            {
                pocoWriter.Write(";");
            }
        }

        #endregion

        #endregion

        #region Class End

        protected virtual void WriteClassEnd(IDbObjectTraverse dbObject, string namespaceOffset)
        {
            pocoWriter.Write(namespaceOffset);
            pocoWriter.WriteLine("}");
        }

        #endregion

        #region Namespace End

        protected virtual void WriteNamespaceEnd()
        {
            if (string.IsNullOrEmpty(Namespace) == false)
                pocoWriter.WriteLine("}");
        }

        #endregion
    }
}
