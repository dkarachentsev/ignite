package org.apache.ignite.yardstick.cache.model;

/**
 *
 */
public class Nds8Key {
    private Long REQUEST_ID;
    private Long DECLARATION_VERSION_ID;

    public Nds8Key(Long REQUEST_ID, Long DECLARATION_VERSION_ID) {
        this.REQUEST_ID = REQUEST_ID;
        this.DECLARATION_VERSION_ID = DECLARATION_VERSION_ID;
    }
}
