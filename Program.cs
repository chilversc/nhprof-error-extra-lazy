using System;

namespace WindowsFormsApplication1
{
    using System.Collections.Generic;
    using FluentNHibernate.Cfg;
    using FluentNHibernate.Cfg.Db;
    using FluentNHibernate.Mapping;
    using HibernatingRhinos.Profiler.Appender;
    using HibernatingRhinos.Profiler.Appender.NHibernate;
    using NHibernate;
    using NHibernate.Cfg;
    using NHibernate.Tool.hbm2ddl;

    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            NHibernateProfiler.Initialize ();

            var assembly = typeof (Program).Assembly;

            var cfg = Fluently.Configure ()
                .Database (SQLiteConfiguration.Standard.InMemory ().AdoNetBatchSize (100))
                .Mappings (m => m.FluentMappings.AddFromAssembly (assembly))
                .BuildConfiguration ();

            var sessionFactory = cfg.BuildSessionFactory ();

            using (var session = sessionFactory.OpenSession ()) {
                long id;

                using (ProfilerIntegration.IgnoreAll ()) {
                    id = Setup (cfg, session);
                }

                using (var tx = session.BeginTransaction ()) {
                    var container = session.Get<Container> (id);
                    container.Children [5].Comment = "testing";
                    tx.Commit ();
                }
            }

            NHibernateProfiler.Stop ();
        }

        private static long Setup (Configuration cfg, ISession session)
        {
            new SchemaExport (cfg).Execute (false, true, false, session.Connection, null);

            var container = new Container ();
            container.Add (new Child (1));
            container.Add (new Child (2));
            container.Add (new Child (3));
            container.Add (new Child (5));
            container.Add (new Child (8));
            container.Add (new Child (13));

            using (var tx = session.BeginTransaction ()) {
                session.Save (container);
                session.Flush ();
                session.Clear ();
                tx.Commit ();
            }

            return container.Id;
        }
    }

    public class Container
    {
        public Container ()
        {
            Children = new Dictionary<int, Child> ();
        }

        public virtual long Id { get; set; }
        public virtual IDictionary<int, Child> Children { get; set; }

        public virtual void Add (Child child)
        {
            child.Container = this;
            Children.Add (child.Key, child);
        }
    }

    public class Child
    {
        public Child ()
        {
        }

        public Child (int key)
        {
            Key = key;
        }

        public virtual long Id { get; set; }
        public virtual int Key { get; set; }
        public virtual string Comment { get; set; }
        public virtual Container Container { get; set; }
    }

    public class ContainerMapping : ClassMap<Container>
    {
        public ContainerMapping ()
        {
            Id (x => x.Id).GeneratedBy.HiLo ("100");
            HasMany (x => x.Children)
                .AsMap (x => x.Key)
                .ExtraLazyLoad ()
                .Inverse ()
                .Cascade.AllDeleteOrphan ();
        }
    }

    public class ChildMapping : ClassMap<Child>
    {
        public ChildMapping ()
        {
            Id (x => x.Id).GeneratedBy.HiLo ("100");
            Map (x => x.Key);
            Map (x => x.Comment);
            References (x => x.Container);
        }
    }
}
