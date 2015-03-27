﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SchemaBuilder.cs" company="Premise Health">
//   Copyright (c) 2015 Premise Health. All rights reserved.
// </copyright>
// <summary>
//   The schema builder.
// </summary>
// --------------------------------------------------------------------------------------------------------------------


namespace Swagger.ObjectModel.Builders
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;

    /// <summary>
    /// The schema builder.
    /// </summary>
    public class SchemaBuilder<TModel> : DataTypeBuilder<SchemaBuilder<TModel>, Schema>
    {
        /// <summary>
        /// The discriminator.
        /// </summary>
        private string discriminator;

        /// <summary>
        /// The read only.
        /// </summary>
        private bool? readOnly;

        /// <summary>
        /// The documentation.
        /// </summary>
        private ExternalDocumentation documentation;

        /// <summary>
        /// The example.
        /// </summary>
        private object example;

        private IDictionary<string, Schema> properties = new Dictionary<string, Schema>();

        private List<string> required = new List<string>();

        private List<string> allOf = new List<string>();

        private string description;

        protected override Schema DataTypeInstance
        {
            get
            {
                base.DataTypeInstance.Discriminator = this.discriminator;
                base.DataTypeInstance.ReadOnly = this.readOnly;
                base.DataTypeInstance.ExternalDocumentation = this.documentation;
                base.DataTypeInstance.Example = this.example;
                base.DataTypeInstance.Properties = this.properties;
                base.DataTypeInstance.AllOf = this.allOf;
                base.DataTypeInstance.Required = this.required;
                base.DataTypeInstance.Description = this.description;

                return base.DataTypeInstance;
            }
        }

        /// <summary>
        /// Access a <see cref="SchemaBuilder{TProperty}"/> for a given property of the model.
        /// </summary>
        /// <param name="expression">An <see cref="Expression{TDelegate}"/> for accessing the property.</param>
        /// <returns>The <see cref="SchemaBuilder{TProperty}"/> instance.</returns>
        public SchemaBuilder<TProperty> Property<TProperty>(Expression<Func<TModel, TProperty>> expression)
        {
            var member = expression.Body as MemberExpression;
            if (member == null)
            {
                throw new ArgumentException("Expression is not a member access", "expression");
            }
            
            var builder = new SchemaBuilder<TProperty>();
            this.properties.Add(member.Member.Name, builder.DataTypeInstance);

            builder.Type(typeof(TProperty).Name);

            return builder;
        }

        /// <summary>
        /// The build.
        /// </summary>
        /// <returns>
        /// The <see cref="Schema"/>.
        /// </returns>
        public override Schema Build()
        {
            return this.DataTypeInstance;
        }

        #region Building

        public SchemaBuilder<TModel> Discriminator(string discriminator)
        {
            this.discriminator = discriminator;
            return this;
        }

        private SchemaBuilder<TModel> Description(string description)
        {
            this.description = description;
            return this;
        }

        public SchemaBuilder<TModel> IsReadOnly()
        {
            this.readOnly = true;
            return this;
        }


        public SchemaBuilder<TModel> ExternalDocumentation(ExternalDocumentation documentation)
        {
            this.documentation = documentation;
            return this;
        }

        public SchemaBuilder<TModel> ExternalDocumentation(ExternalDocumentationBuilder documentation)
        {
            this.documentation = documentation.Build();
            return this;
        }

        public SchemaBuilder<TModel> Example(object example)
        {
            this.example = example;
            return this;
        }

        #endregion

        private static bool IsImplicitlyRequired(Type type)
        {
            return type.IsValueType && !IsNullable(type);
        }

        private static bool IsNullable(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        private static bool IsClassProperty(Type propertyType)
        {
            return !Primitive.IsPrimitive(propertyType) && !propertyType.IsEnum && !propertyType.IsGenericType;
        }

        public static Schema ToSchema<T>(T modelType)
        {
            var modelProperties = typeof(T).GetProperties();
            
            var schema = new Schema();

            foreach (var modelProperty in modelProperties)
            {
                var propertyType = modelProperty.GetType();
                var property = new Schema();

                var id = SwaggerBuilderConfig.ModelIdConvention(propertyType);
                string description = null;
                var required = IsImplicitlyRequired(propertyType);
                var properties = propertyType.GetProperties().ToDictionary(prop => prop.Name, ToSchema);

                property.Required = required;
                property.Properties = new Dictionary<string, Schema>();
                schema.Properties.Add(modelProperty.Name, property);
            }
            
        }

        private static Schema FromClass<T>(T classType)
        {
            
        }

        public static IEnumerable<Schema> ToModel<T>(T modelType, IEnumerable<Schema> knownModels = null)
        {
            var modelProperties = typeof(T).GetProperties();
            var classProperties = modelProperties.Where(x => !Primitive.IsPrimitive(x.PropertyType) && !x.PropertyType.IsEnum && !x.PropertyType.IsGenericType);

            var modelsData = knownModels ?? Enumerable.Empty<Schema>();

            foreach (var property in classProperties)
            {
                var properties = property.GetType().GetProperties();

                var existingSchemaForClassProperty = modelsData.FirstOrDefault(x => x.ClrType == property.GetType());

                var id = existingSchemaForClassProperty == null
                    ? property.GetType().Name
                    : SwaggerConfig.ModelIdConvention(existingSchemaForClassProperty.ClrType);

                var description = existingSchemaForClassProperty == null
                    ? null
                    : existingSchemaForClassProperty.Description;

                var required = existingSchemaForClassProperty == null
                    ? new List<string>()
                    : existingSchemaForClassProperty.Properties
                        .Where(p => p.Value.Required.Contains(p.Key) || IsImplicitlyRequired(p.Value.ClrType))
                        .Select(p => p.Key)
                        .OrderBy(name => name)
                        .ToList();

                
                var modelproperties = properties.OrderBy(x => x.Name).ToDictionary(x => x.Name, x => ToModel(x.GetType()))

                yield return new Model
                {
                    Id = id,
                    Description = description,
                    Required = required,
                    Properties = modelproperties
                };
            }

            var topLevelModel = new Model
            {
                Id = SwaggerConfig.ModelIdConvention(model.ModelType),
                Description = model.Description,
                Required = model.Properties
                    .Where(p => p.Required || p.Type.IsImplicitlyRequired())
                    .Select(p => p.Name)
                    .OrderBy(name => name)
                    .ToList(),
                Properties = model.Properties
                    .OrderBy(p => p.Name)
                    .ToDictionary(p => p.Name, ToModelProperty)

                // TODO: SubTypes and Discriminator
            };

            yield return topLevelModel;
        }
    }
}