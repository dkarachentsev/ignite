/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

package org.apache.ignite.spark

import org.apache.ignite.Ignite
import org.apache.ignite.configuration.{CacheConfiguration, IgniteConfiguration}
import org.apache.ignite.internal.IgnitionEx
import org.apache.ignite.lang.IgniteOutClosure
import org.apache.spark.api.java.JavaSparkContext

import scala.reflect.ClassTag

/**
 * Java-friendly Ignite context wrapper.
 *
 * @param sc Java Spark context.
 * @param cfgF Configuration factory.
 * @tparam K Key type.
 * @tparam V Value type.
 */
class JavaIgniteContext[K, V](
    @scala.transient val sc: JavaSparkContext,
    val cfgF: IgniteOutClosure[IgniteConfiguration]) extends Serializable {

    @transient val ic: IgniteContext[K, V] = new IgniteContext[K, V](sc.sc, () => cfgF.apply())

    def this(sc: JavaSparkContext, springUrl: String) {
        this(sc, new IgniteOutClosure[IgniteConfiguration] {
            override def apply() = IgnitionEx.loadConfiguration(springUrl).get1()
        })
    }

    def fromCache(cacheName: String): JavaIgniteRDD[K, V] =
        JavaIgniteRDD.fromIgniteRDD(new IgniteRDD[K, V](ic, cacheName, null))

    def fromCache(cacheCfg: CacheConfiguration[K, V]) =
        JavaIgniteRDD.fromIgniteRDD(new IgniteRDD[K, V](ic, cacheCfg.getName, cacheCfg))

    def ignite(): Ignite = ic.ignite()

    def close() = ic.close()

    private[spark] def fakeClassTag[T]: ClassTag[T] = ClassTag.AnyRef.asInstanceOf[ClassTag[T]]

    implicit val ktag: ClassTag[K] = fakeClassTag

    implicit val vtag: ClassTag[V] = fakeClassTag
}
