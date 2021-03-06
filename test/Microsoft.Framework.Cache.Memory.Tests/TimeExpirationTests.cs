﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Threading;
using Microsoft.AspNet.Testing;
using Microsoft.Framework.Cache.Memory.Infrastructure;
using Xunit;

namespace Microsoft.Framework.Cache.Memory
{
    public class TimeExpirationTests
    {
        private IMemoryCache CreateCache(ISystemClock clock)
        {
            return new MemoryCache(new MemoryCacheOptions()
            {
                Clock = clock,
                ListenForMemoryPressure = false,
            });
        }

        [Fact]
        public void AbsoluteExpirationInThePastThrows()
        {
            var clock = new TestClock();
            var cache = CreateCache(clock);
            var key = "myKey";
            var obj = new object();

            var expected = clock.UtcNow - TimeSpan.FromMinutes(1);
            ExceptionAssert.ThrowsArgumentOutOfRange(() =>
            {
                var result = cache.Set(key, context =>
                {
                    context.SetAbsoluteExpiration(expected);
                    return obj;
                });
            }, "absolute", "The absolute expiration value must be in the future.", expected.ToString(CultureInfo.CurrentCulture));
        }

        [Fact]
        public void AbsoluteExpirationExpires()
        {
            var clock = new TestClock();
            var cache = CreateCache(clock);
            var key = "myKey";
            var obj = new object();

            var result = cache.Set(key, context =>
            {
                context.SetAbsoluteExpiration(clock.UtcNow + TimeSpan.FromMinutes(1));
                return obj;
            });
            Assert.Same(obj, result);

            var found = cache.TryGetValue(key, out result);
            Assert.True(found);
            Assert.Same(obj, result);

            clock.Add(TimeSpan.FromMinutes(2));

            found = cache.TryGetValue(key, out result);
            Assert.False(found);
            Assert.Null(result);
        }

        [Fact]
        public void AbsoluteExpirationExpiresInBackground()
        {
            var clock = new TestClock();
            var cache = CreateCache(clock);
            var key = "myKey";
            var obj = new object();
            var callbackInvoked = new ManualResetEvent(false);

            var result = cache.Set(key, context =>
            {
                context.SetAbsoluteExpiration(clock.UtcNow + TimeSpan.FromMinutes(1));
                context.RegisterPostEvictionCallback((subkey, value, reason, state) =>
                {
                    // TODO: Verify params
                    var localCallbackInvoked = (ManualResetEvent)state;
                    localCallbackInvoked.Set();
                }, state: callbackInvoked);
                return obj;
            });
            Assert.Same(obj, result);

            var found = cache.TryGetValue(key, out result);
            Assert.True(found);
            Assert.Same(obj, result);

            clock.Add(TimeSpan.FromMinutes(2));
            var ignored = cache.Get("otherKey"); // Background expiration checks are triggered by misc cache activity.

            Assert.True(callbackInvoked.WaitOne(100), "Callback");

            found = cache.TryGetValue(key, out result);
            Assert.False(found);
            Assert.Null(result);
        }

        [Fact]
        public void NegativeRelativeExpirationThrows()
        {
            var clock = new TestClock();
            var cache = CreateCache(clock);
            var key = "myKey";
            var obj = new object();

            ExceptionAssert.ThrowsArgumentOutOfRange(() =>
            {
                var result = cache.Set(key, context =>
                {
                    context.SetAbsoluteExpiration(TimeSpan.FromMinutes(-1));
                    return obj;
                });
            }, "relative", "The relative expiration value must be positive.", TimeSpan.FromMinutes(-1));
        }

        [Fact]
        public void ZeroRelativeExpirationThrows()
        {
            var clock = new TestClock();
            var cache = CreateCache(clock);
            var key = "myKey";
            var obj = new object();

            ExceptionAssert.ThrowsArgumentOutOfRange(() =>
            {
                var result = cache.Set(key, context =>
                {
                    context.SetAbsoluteExpiration(TimeSpan.Zero);
                    return obj;
                });
            }, "relative", "The relative expiration value must be positive.", TimeSpan.Zero);
        }

        [Fact]
        public void RelativeExpirationExpires()
        {
            var clock = new TestClock();
            var cache = CreateCache(clock);
            var key = "myKey";
            var obj = new object();

            var result = cache.Set(key, context =>
            {
                context.SetAbsoluteExpiration(TimeSpan.FromMinutes(1));
                return obj;
            });
            Assert.Same(obj, result);

            var found = cache.TryGetValue(key, out result);
            Assert.True(found);
            Assert.Same(obj, result);

            clock.Add(TimeSpan.FromMinutes(2));

            found = cache.TryGetValue(key, out result);
            Assert.False(found);
            Assert.Null(result);
        }

        [Fact]
        public void NegativeSlidingExpirationThrows()
        {
            var clock = new TestClock();
            var cache = CreateCache(clock);
            var key = "myKey";
            var obj = new object();

            ExceptionAssert.ThrowsArgumentOutOfRange(() =>
            {
                var result = cache.Set(key, context =>
                {
                    context.SetSlidingExpiration(TimeSpan.FromMinutes(-1));
                    return obj;
                });
            }, "offset", "The sliding expiration value must be positive.", TimeSpan.FromMinutes(-1));
        }

        [Fact]
        public void ZeroSlidingExpirationThrows()
        {
            var clock = new TestClock();
            var cache = CreateCache(clock);
            var key = "myKey";
            var obj = new object();

            ExceptionAssert.ThrowsArgumentOutOfRange(() =>
            {
                var result = cache.Set(key, context =>
                {
                    context.SetSlidingExpiration(TimeSpan.Zero);
                    return obj;
                });
            }, "offset", "The sliding expiration value must be positive.", TimeSpan.Zero);
        }

        [Fact]
        public void SlidingExpirationExpiresIfNotAccessed()
        {
            var clock = new TestClock();
            var cache = CreateCache(clock);
            var key = "myKey";
            var obj = new object();

            var result = cache.Set(key, context =>
            {
                context.SetSlidingExpiration(TimeSpan.FromMinutes(1));
                return obj;
            });
            Assert.Same(obj, result);

            var found = cache.TryGetValue(key, out result);
            Assert.True(found);
            Assert.Same(obj, result);

            clock.Add(TimeSpan.FromMinutes(2));

            found = cache.TryGetValue(key, out result);
            Assert.False(found);
            Assert.Null(result);
        }

        [Fact]
        public void SlidingExpirationRenewedByAccess()
        {
            var clock = new TestClock();
            var cache = CreateCache(clock);
            var key = "myKey";
            var obj = new object();

            var result = cache.Set(key, context =>
            {
                context.SetSlidingExpiration(TimeSpan.FromMinutes(1));
                return obj;
            });
            Assert.Same(obj, result);

            var found = cache.TryGetValue(key, out result);
            Assert.True(found);
            Assert.Same(obj, result);

            for (int i = 0; i < 10; i++)
            {
                clock.Add(TimeSpan.FromSeconds(15));

                found = cache.TryGetValue(key, out result);
                Assert.True(found);
                Assert.Same(obj, result);
            }
        }

        [Fact]
        public void SlidingExpirationRenewedByAccessUntilAbsoluteExpiration()
        {
            var clock = new TestClock();
            var cache = CreateCache(clock);
            var key = "myKey";
            var obj = new object();

            var result = cache.Set(key, context =>
            {
                context.SetSlidingExpiration(TimeSpan.FromMinutes(1));
                context.SetAbsoluteExpiration(TimeSpan.FromMinutes(2));
                return obj;
            });
            Assert.Same(obj, result);

            var found = cache.TryGetValue(key, out result);
            Assert.True(found);
            Assert.Same(obj, result);

            for (int i = 0; i < 7; i++)
            {
                clock.Add(TimeSpan.FromSeconds(15));

                found = cache.TryGetValue(key, out result);
                Assert.True(found);
                Assert.Same(obj, result);
            }

            clock.Add(TimeSpan.FromSeconds(15));

            found = cache.TryGetValue(key, out result);
            Assert.False(found);
            Assert.Null(result);
        }
    }
}