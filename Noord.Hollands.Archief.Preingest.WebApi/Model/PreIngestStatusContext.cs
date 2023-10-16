using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Noord.Hollands.Archief.Preingest.WebApi.Entities.Context;

namespace Noord.Hollands.Archief.Preingest.WebApi.Model
{
    /// <summary>
    /// Preingest database context class
    /// </summary>
    /// <seealso cref="Microsoft.EntityFrameworkCore.DbContext" />
    public class PreIngestStatusContext : DbContext
    {
        /// <summary>
        /// Gets or sets the action state collection.
        /// </summary>
        /// <value>
        /// The action state collection.
        /// </value>
        public DbSet<ActionStates> ActionStateCollection { get; set; }
        /// <summary>
        /// Gets or sets the preingest action collection.
        /// </summary>
        /// <value>
        /// The preingest action collection.
        /// </value>
        public DbSet<PreingestAction> PreingestActionCollection { get; set; }
        /// <summary>
        /// Gets or sets the action state message collection.
        /// </summary>
        /// <value>
        /// The action state message collection.
        /// </value>
        public DbSet<StateMessage> ActionStateMessageCollection { get; set; }
        /// <summary>
        /// Gets or sets the execution plan collection.
        /// </summary>
        /// <value>
        /// The execution plan collection.
        /// </value>
        public DbSet<ExecutionPlan> ExecutionPlanCollection { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PreIngestStatusContext"/> class.
        /// </summary>
        public PreIngestStatusContext() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="PreIngestStatusContext"/> class.
        /// </summary>
        /// <param name="options">The options.</param>
        public PreIngestStatusContext(DbContextOptions<PreIngestStatusContext> options)
            : base(options) {  }

        /// <summary>
        /// Override this method to further configure the model that was discovered by convention from the entity types
        /// exposed in <see cref="T:Microsoft.EntityFrameworkCore.DbSet`1" /> properties on your derived context. The resulting model may be cached
        /// and re-used for subsequent instances of your derived context.
        /// </summary>
        /// <param name="modelBuilder">The builder being used to construct the model for this context. Databases (and other extensions) typically
        /// define extension methods on this object that allow you to configure aspects of the model that are specific
        /// to a given database.</param>
        /// <remarks>
        /// If a model is explicitly set on the options for this context (via <see cref="M:Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.UseModel(Microsoft.EntityFrameworkCore.Metadata.IModel)" />)
        /// then this method will not be run.
        /// </remarks>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PreingestAction>().ToTable("Actions");
            modelBuilder.Entity<ActionStates>().ToTable("States");
            modelBuilder.Entity<StateMessage>().ToTable("Messages");
            modelBuilder.Entity<ExecutionPlan>().ToTable("Plan");
            modelBuilder.Entity<ExecutionPlan>().Property(e => e.RecordId).ValueGeneratedOnAdd();
        }

        /// <summary>
        /// <para>
        /// Override this method to configure the database (and other options) to be used for this context.
        /// This method is called for each instance of the context that is created.
        /// The base implementation does nothing.
        /// </para>
        /// <para>
        /// In situations where an instance of <see cref="T:Microsoft.EntityFrameworkCore.DbContextOptions" /> may or may not have been passed
        /// to the constructor, you can use <see cref="P:Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.IsConfigured" /> to determine if
        /// the options have already been set, and skip some or all of the logic in
        /// <see cref="M:Microsoft.EntityFrameworkCore.DbContext.OnConfiguring(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder)" />.
        /// </para>
        /// </summary>
        /// <param name="optionsBuilder">A builder used to create or modify options for this context. Databases (and other extensions)
        /// typically define extension methods on this object that allow you to configure the context.</param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                IConfigurationRoot configuration = new ConfigurationBuilder()
                   .SetBasePath(Directory.GetCurrentDirectory())
                   // Probably unexpected: in Program.cs this uses `optional: true, reloadOnChange: true`, and an
                   // optional environment-specific JSON file is loaded too
                   .AddJsonFile("appsettings.json")
                   .AddEnvironmentVariables()
                   .Build();
                var connectionString = configuration.GetConnectionString("Sqlite");
                optionsBuilder.UseSqlite(connectionString);
            }
        }
    }
}
