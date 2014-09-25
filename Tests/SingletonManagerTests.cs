﻿using Core;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Utils;
using Xunit;

namespace Utils.Tests
{
    public class SingletonManagerTests
    {
        Mock<ILogger> mockLogger;
        Mock<IFactory> mockFactory;
        Mock<IReflection> mockReflection;
        SingletonManager testObject;

        void Init()
        {
            mockFactory = new Mock<IFactory>();
            mockReflection = new Mock<IReflection>();
            mockLogger = new Mock<ILogger>();
            testObject = new SingletonManager(mockReflection.Object, mockLogger.Object);
        }

        private void InitTestSingleton(Type singletonType, object singleton, params string[] dependencyNames)
        {
            testObject.RegisterSingleton(new SingletonDef()
            {
                singletonType = singletonType,
                dependencyNames = dependencyNames
            });

            mockFactory
                .Setup(m => m.Create(singletonType))
                .Returns(singleton);
        }

        [Fact]
        public void init_singletons_when_there_are_no_singletons()
        {
            Init();

            testObject.InstantiateSingletons(mockFactory.Object);

            Assert.Empty(testObject.Singletons);
        }

        [Fact]
        public void can_find_instantiate_registered_singleton()
        {
            Init();

            var singletonType = typeof(object); // Anything will do here.
            var singleton = new object();
            InitTestSingleton(singletonType, singleton);

            testObject.InstantiateSingletons(mockFactory.Object);

            Assert.Equal(1, testObject.Singletons.Length);
            Assert.Equal(singleton, testObject.Singletons[0]);
        }

        [Fact]
        public void lazy_singleton_is_instantiated_on_resolve()
        {
            Init();

            var singletonType = typeof(object); // Anything will do here.
            var dependencyName = "dep";

            testObject.RegisterSingleton(new SingletonDef()
            {
                singletonType = singletonType,
                dependencyNames = new string[] { dependencyName },
                lazy = true
            });

            testObject.InstantiateSingletons(mockFactory.Object);

            var singleton = new object();
            mockFactory
                .Setup(m => m.Create(singletonType))
                .Returns(singleton);

            Assert.Equal(singleton, testObject.ResolveDependency(dependencyName, mockFactory.Object));
        }

        [Fact]
        public void subsequent_resolve_of_lazy_singleton_gets_cached_object()
        {
            Init();

            var singletonType = typeof(object); // Anything will do here.
            var dependencyName = "dep";

            testObject.RegisterSingleton(new SingletonDef()
            {
                singletonType = singletonType,
                dependencyNames = new string[] { dependencyName },
                lazy = true
            });

            testObject.InstantiateSingletons(mockFactory.Object);

            var singleton = new object();
            mockFactory
                .Setup(m => m.Create(singletonType))
                .Returns(singleton);

            Assert.Equal(singleton, testObject.ResolveDependency(dependencyName, mockFactory.Object));
            Assert.Equal(singleton, testObject.ResolveDependency(dependencyName, mockFactory.Object));

            mockFactory.Verify(m => m.Create(singletonType), Times.Once());
        }

        [Fact]
        public void non_startable_singletons_are_ignored_on_start()
        {
            Init();

            var singletonType = typeof(object); // Anything will do here.
            var nonStartableSingleton = new object();
            InitTestSingleton(singletonType, nonStartableSingleton);

            testObject.InstantiateSingletons(mockFactory.Object);

            testObject.Start();
        }

        [Fact]
        public void non_startable_singletons_are_ignored_on_shutdown()
        {
            Init();

            var singletonType = typeof(object); // Anything will do here.
            var nonStartableSingleton = new object();
            InitTestSingleton(singletonType, nonStartableSingleton);

            testObject.InstantiateSingletons(mockFactory.Object);

            testObject.Shutdown();
        }

        [Fact]
        public void resolving_non_existant_singleton_returns_null()
        {
            Init();

            Assert.Null(testObject.ResolveDependency("some singleton that doesnt exist", mockFactory.Object));
        }

        [Fact]
        public void can_resolve_singleton_as_dependency()
        {
            Init();

            var singletonType = typeof(object); // Anything will do here.
            var singleton = new object();
            var dependencyName = "dep1";
            InitTestSingleton(singletonType, singleton, dependencyName);

            testObject.InstantiateSingletons(mockFactory.Object);

            Assert.Equal(singleton, testObject.ResolveDependency(dependencyName, mockFactory.Object));
        }

        [Fact]
        public void can_start_singletons()
        {
            Init();

            var singletonType = typeof(object); // Anything will do here.
            var mockStartableSingleton = new Mock<IStartable>();
            InitTestSingleton(singletonType, mockStartableSingleton.Object);

            testObject.InstantiateSingletons(mockFactory.Object);

            testObject.Start();

            mockStartableSingleton.Verify(m => m.Start(), Times.Once());
        }

        [Fact]
        public void can_shutdown_singletons()
        {
            Init();

            var singletonType = typeof(object); // Anything will do here.
            var mockStartableSingleton = new Mock<IStartable>();
            InitTestSingleton(singletonType, mockStartableSingleton.Object);

            testObject.InstantiateSingletons(mockFactory.Object);

            testObject.Shutdown();

            mockStartableSingleton.Verify(m => m.Shutdown(), Times.Once());
        }

        [Fact]
        public void start_exceptions_are_swallowed_and_logged()
        {
            Init();

            var singletonType = typeof(object); // Anything will do here.
            var mockStartableSingleton = new Mock<IStartable>();
            InitTestSingleton(singletonType, mockStartableSingleton.Object);
            testObject.InstantiateSingletons(mockFactory.Object);

            mockStartableSingleton
                .Setup(m => m.Start())
                .Throws<ApplicationException>();

            Assert.DoesNotThrow(() => 
                testObject.Start()
            );

            mockLogger.Verify(m => m.LogError(It.IsAny<string>(), It.IsAny<ApplicationException>()), Times.Once());            
        }

        [Fact]
        public void shutdown_exceptions_are_swallowed_and_logged()
        {
            Init();

            var singletonType = typeof(object); // Anything will do here.
            var mockStartableSingleton = new Mock<IStartable>();
            InitTestSingleton(singletonType, mockStartableSingleton.Object);

            testObject.InstantiateSingletons(mockFactory.Object);

            mockStartableSingleton
                .Setup(m => m.Shutdown())
                .Throws<ApplicationException>();

            Assert.DoesNotThrow(() =>
                testObject.Shutdown()
            );

            mockLogger.Verify(m => m.LogError(It.IsAny<string>(), It.IsAny<ApplicationException>()), Times.Once());
        }

        private static SingletonDef InitDef(Type singletonType, params Type[] interfaceTypes)
        {
            return new SingletonDef()
            {
                singletonType = singletonType,
                dependencyNames = interfaceTypes.Select(type => type.Name).ToArray()
            };
        }

        public class can_order_types_based_on_dependencies_when_types_are_already_in_correct_order
        {
            public interface ITest1
            {

            }

            public class Test1 : ITest1
            {

            }

            public class Test2
            {
                [Dependency]
                public ITest1 Test1 { get; set; }
            }

            [Fact(Timeout = 1000)]
            public void test()
            {
                var mockFactory = new Mock<IFactory>();

                var def1 = InitDef(typeof(Test1), typeof(ITest1));
                var def2 = InitDef(typeof(Test2));
                var singletonDefs = new SingletonDef[] { def1, def2 };

                var sorted = SingletonManager.OrderByDeps(singletonDefs, mockFactory.Object).ToArray();

                Assert.Equal(2, sorted.Length);
                Assert.Equal(def1, sorted[0]);
                Assert.Equal(def2, sorted[1]);
            }
        }

        public class can_order_types_based_on_dependencies_with_two_types_out_of_order
        {
            public interface ITest1
            {

            }

            public class Test1 : ITest1
            {

            }

            public class Test2
            {
                [Dependency]
                public ITest1 Test1 { get; set; }
            }

            [Fact(Timeout = 1000)]
            public void test()
            {
                var mockFactory = new Mock<IFactory>();

                var def1 = InitDef(typeof(Test2));
                var def2 = InitDef(typeof(Test1), typeof(ITest1));
                var singletonDefs = new SingletonDef[] { def1, def2 };

                var sorted = SingletonManager.OrderByDeps(singletonDefs, mockFactory.Object).ToArray();

                Assert.Equal(2, sorted.Length);
                Assert.Equal(def2, sorted[0]);
                Assert.Equal(def1, sorted[1]);
            }
        }

        public class can_order_types_based_on_dependencies_with_multiple_dependent_types
        {
            public interface ITest1
            {

            }

            public class Test1 : ITest1
            {

            }

            public interface ITest2
            {

            }

            public class Test2 : ITest2
            {

            }

            public class Test3
            {
                [Dependency]
                public ITest1 Test1 { get; set; }

                [Dependency]
                public ITest2 Test2 { get; set; }
            }

            [Fact(Timeout = 1000)]
            public void test()
            {
                var mockFactory = new Mock<IFactory>();

                var def1 = InitDef(typeof(Test1), typeof(ITest1));
                var def2 = InitDef(typeof(Test3));
                var def3 = InitDef(typeof(Test2), typeof(ITest2));
                var singletonDefs = new SingletonDef[] { def1, def2, def3 };

                var sorted = SingletonManager.OrderByDeps(singletonDefs, mockFactory.Object).ToArray();

                Assert.Equal(3, sorted.Length);
                Assert.Equal(def1, sorted[0]);
                Assert.Equal(def3, sorted[1]);
                Assert.Equal(def2, sorted[2]);
            }
        }

        public class can_order_types_based_on_property_dependencies_with_intermediate_type
        {
            public interface ITest1
            {

            }

            public class Test1 : ITest1
            {

            }

            public interface ITest2
            {

            }

            public class Test2 : ITest2
            {
                [Dependency]
                public ITest1 Test1 { get; set; }
            }

            public class Test3
            {
                [Dependency]
                public ITest2 Test2 { get; set; }
            }

            [Fact(Timeout = 1000)]
            public void test()
            {
                var mockFactory = new Mock<IFactory>();

                mockFactory
                    .Setup(m => m.FindType(typeof(ITest2).Name))
                    .Returns(typeof(Test2));

                var def1 = InitDef(typeof(Test3));
                var def2 = InitDef(typeof(Test1), typeof(ITest1));
                var singletonDefs = new SingletonDef[] { def1, def2 };

                var sorted = SingletonManager.OrderByDeps(singletonDefs, mockFactory.Object).ToArray();

                Assert.Equal(2, sorted.Length);
                Assert.Equal(def2, sorted[0]);
                Assert.Equal(def1, sorted[1]);
            }
        }
        public class can_order_types_with_unspecified_dependencies
        {
            public interface ITest1
            {

            }

            public class Test2
            {
            }

            public class Test3
            {
                [Dependency]
                public ITest1 Test1 { get; set; }
            }

            [Fact(Timeout = 1000)]
            public void test()
            {
                var mockFactory = new Mock<IFactory>();

                var def1 = InitDef(typeof(Test2));
                var def2 = InitDef(typeof(Test3));
                var singletonDefs = new SingletonDef[] { def1, def2 };

                var sorted = SingletonManager.OrderByDeps(singletonDefs, mockFactory.Object).ToArray();

                Assert.Equal(2, sorted.Length);
                Assert.Equal(def1, sorted[0]);
                Assert.Equal(def2, sorted[1]);
            }

            [Fact(Timeout = 1000)]
            public void reverse()
            {
                var mockFactory = new Mock<IFactory>();

                var def1 = InitDef(typeof(Test3));
                var def2 = InitDef(typeof(Test2));
                var singletonDefs = new SingletonDef[] { def1, def2 };

                var sorted = SingletonManager.OrderByDeps(singletonDefs, mockFactory.Object).ToArray();

                Assert.Equal(2, sorted.Length);
                Assert.Equal(def1, sorted[0]);
                Assert.Equal(def2, sorted[1]);
            }
        }

        public class can_order_types_based_on_constructor_dependencies_with_multiple_dependent_types
        {
            public interface ITest1
            {

            }

            public class Test1 : ITest1
            {

            }

            public interface ITest2
            {

            }

            public class Test2 : ITest2
            {

            }

            public class Test3
            {
                public Test3(ITest1 test1, ITest2 test2)
                {
                    this.Test1 = test1;
                    this.Test2 = Test2;
                }

                public ITest1 Test1 { get; set; }
                public ITest2 Test2 { get; set; }
            }

            [Fact(Timeout = 1000)]
            public void test()
            {
                var types = new Type[] { typeof(Test1), typeof(Test3), typeof(Test2) };

                var mockLogger = new Mock<ILogger>();
                var mockFactory = new Mock<IFactory>();

                var def1 = InitDef(typeof(Test1), typeof(ITest1));
                var def2 = InitDef(typeof(Test3));
                var def3 = InitDef(typeof(Test2), typeof(ITest2));
                var singletonDefs = new SingletonDef[] { def1, def2, def3 };
                                
                var sorted = SingletonManager.OrderByDeps(singletonDefs, mockFactory.Object).ToArray();

                Assert.Equal(3, sorted.Length);
                Assert.Equal(def1, sorted[0]);
                Assert.Equal(def3, sorted[1]);
                Assert.Equal(def2, sorted[2]);
            }
        }

        public class throws_when_attempting_to_order_types_that_have_circular_dependency
        {
            public interface ITest1
            {

            }

            public class Test1 : ITest1
            {
                [Dependency]
                public ITest2 Test2 { get; set; }
            }

            public interface ITest2
            {

            }

            public class Test2 : ITest2
            {
                [Dependency]
                public ITest1 Test1 { get; set; }
            }

            [Fact(Timeout = 1000)]
            public void test()
            {
                var mockFactory = new Mock<IFactory>();

                var def1 = InitDef(typeof(Test1), typeof(ITest1));
                var def2 = InitDef(typeof(Test2), typeof(ITest2));
                var singletonDefs = new SingletonDef[] { def1, def2 };

                Assert.Throws<ApplicationException>(() =>
                    SingletonManager.OrderByDeps(singletonDefs, mockFactory.Object)
                );
            }
        }
    }
}
