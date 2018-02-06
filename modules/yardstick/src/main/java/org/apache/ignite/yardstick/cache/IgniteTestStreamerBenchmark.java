package org.apache.ignite.yardstick.cache;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Random;
import java.util.concurrent.Callable;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.atomic.AtomicBoolean;
import org.apache.ignite.IgniteDataStreamer;
import org.apache.ignite.yardstick.IgniteAbstractBenchmark;
import org.apache.ignite.yardstick.cache.model.Nds8;
import org.yardstickframework.BenchmarkConfiguration;
import org.yardstickframework.BenchmarkUtils;

/**
 *
 */
public class IgniteTestStreamerBenchmark extends IgniteAbstractBenchmark {
    /** */
    private static final String CACHE_NAME = "nds8";

    /** */
    private ExecutorService executor;

    /** */
    private int entries;

    /** {@inheritDoc} */
    @Override public void setUp(BenchmarkConfiguration cfg) throws Exception {
        super.setUp(cfg);

        if (!ignite().active())
            ignite().active(true);

        entries = args.range();

        if (entries <= 0)
            throw new IllegalArgumentException("Invalid number of entries: " + entries);

        if (cfg.threads() != 1)
            throw new IllegalArgumentException("IgniteStreamerBenchmark should be run with single thread. " +
                "Internally it starts multiple threads.");

        executor = Executors.newFixedThreadPool(args.streamerThreads());

        BenchmarkUtils.println("IgniteStreamerBenchmark start [cacheIndex=" + args.streamerCacheIndex() +
            ", concurrentCaches=" + args.streamerConcurrentCaches() +
            ", entries=" + entries +
            ", bufferSize=" + args.streamerBufferSize() +
            ", cacheToUse=" + CACHE_NAME + ']');
    }

    /** {@inheritDoc} */
    @Override public boolean test(Map<Object, Object> map) throws Exception {
        BenchmarkUtils.println("IgniteStreamerBenchmark start test.");

        long start = System.currentTimeMillis();

        final AtomicBoolean stop = new AtomicBoolean();

        try {
            final int threads = args.streamerThreads();

            List<Future<Void>> futs = new ArrayList<>(threads);
            try (IgniteDataStreamer<Object, Object> streamer = ignite().dataStreamer(CACHE_NAME)) {

                if (args.streamerParOps() > 0)
                    streamer.perNodeParallelOperations(args.streamerParOps());

                streamer.perNodeBufferSize(args.streamerBufferSize());

                for (int i = 0; i < threads; i++) {
                    futs.add(executor.submit(new Callable<Void>() {
                        @Override public Void call() throws Exception {
                            Thread.currentThread().setName("streamer-" + CACHE_NAME);

                            long start = System.currentTimeMillis();

                            Random rnd = new Random();

                            BenchmarkUtils.println("IgniteStreamerBenchmark start load cache [name=" + CACHE_NAME + ']');

                            int lim = entries / threads;

                            for (int i = 0; i < lim; i++) {
                                Nds8 nds = Nds8.generate(rnd.nextLong(), rnd.nextLong());

                                streamer.addData(nds.getNds8Key(), nds);

                                if (i > 0 && i % 1000 == 0) {
                                    if (stop.get())
                                        break;

                                    if (i % 100_000 == 0) {
                                        BenchmarkUtils.println("IgniteStreamerBenchmark cache load progress [name=" + CACHE_NAME +
                                            ", entries=" + i +
                                            ", timeMillis=" + (System.currentTimeMillis() - start) + ']');
                                    }
                                }
                            }

                            long time = System.currentTimeMillis() - start;

                            BenchmarkUtils.println("IgniteStreamerBenchmark finished load cache [name=" + CACHE_NAME +
                                ", entries=" + entries +
                                ", bufferSize=" + args.streamerBufferSize() +
                                ", totalTimeMillis=" + time + ']');

                            return null;
                        }
                    }));

                    for (Future<Void> fut : futs)
                        fut.get();
                }
            }
        }
        finally {
            stop.set(true);
        }

        long time = System.currentTimeMillis() - start;

        BenchmarkUtils.println("IgniteStreamerBenchmark finished [totalTimeMillis=" + time +
            ", entries=" + entries +
            ", bufferSize=" + args.streamerBufferSize() +
            ", cache size [cacheName=" + CACHE_NAME +
            ", size=" + ignite().cache(CACHE_NAME).size()+ ']');

        return false;
    }

    /** {@inheritDoc} */
    @Override public void tearDown() throws Exception {
        if (executor != null)
            executor.shutdown();

        super.tearDown();
    }
}
