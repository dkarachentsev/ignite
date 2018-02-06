package org.apache.ignite.yardstick.cache.model;

import java.math.BigDecimal;
import java.sql.Date;

/**
 *
 */
public class Nds8 {
    private Long REQUEST_ID;
    private Long DECLARATION_VERSION_ID;
    private Integer CHAPTER;
    private Long ORDINAL_NUMBER;
    private String OKV_CODE;
    private Date CREATE_DATE;
    private Date RECEIVE_DATE;
    private String OPERATION_CODE;
    private String INVOICE_NUM;
    private Date INVOICE_DATE;
    private Short CHANGE_NUM;
    private Date CHANGE_DATE;
    private String CORRECTION_NUM;
    private Date CORRECTION_DATE;
    private Short CHANGE_CORRECTION_NUM;
    private Date CHANGE_CORRECTION_DATE;
    private String RECEIPT_DOC_NUM;
    private String RECEIPT_DOC_DATE;
    private String BUY_ACCEPT_DATE;
    private String BUYER_INN;
    private String BUYER_KPP;
    private String SELLER_INN;
    private String SELLER_KPP;
    private String SELLER_INVOICE_NUM;
    private Date SELLER_INVOICE_DATE;
    private String BROKER_INN;
    private String BROKER_KPP;
    private Integer DEAL_KIND_CODE;
    private String CUSTOMS_DECLARATION_NUM;
    private BigDecimal PRICE_BUY_AMOUNT;
    private BigDecimal PRICE_BUY_NDS_AMOUNT;
    private BigDecimal PRICE_SELL;
    private BigDecimal PRICE_SELL_IN_CURR;
    private BigDecimal PRICE_SELL_18;
    private BigDecimal PRICE_SELL_10;
    private BigDecimal PRICE_SELL_0;
    private BigDecimal PRICE_NDS_18;
    private BigDecimal PRICE_NDS_10;
    private BigDecimal PRICE_TAX_FREE;
    private BigDecimal PRICE_TOTAL;
    private BigDecimal PRICE_NDS_TOTAL;
    private BigDecimal DIFF_CORRECT_DECREASE;
    private BigDecimal DIFF_CORRECT_INCREASE;
    private BigDecimal DIFF_CORRECT_NDS_DECREASE;
    private BigDecimal DIFF_CORRECT_NDS_INCREASE;
    private BigDecimal PRICE_NDS_BUYER;
    private String ROW_KEY;
    private String ACTUAL_ROW_KEY;
    private String COMPARE_ROW_KEY;
    private Integer COMPARE_ALGO_ID;
    private String FORMAT_ERRORS;
    private String LOGICAL_ERRORS;
    private String SELLER_AGENCY_INFO_INN;
    private String SELLER_AGENCY_INFO_KPP;
    private String SELLER_AGENCY_INFO_NAME;
    private String SELLER_AGENCY_INFO_NUM;
    private Date SELLER_AGENCY_INFO_DATE;
    private Integer IS_DOP_LIST;
    private Integer IS_IMPORT;
    private String CLARIFICATION_KEY;
    private Long CONTRAGENT_KEY;
    private Integer IS_CLARIFIED;
    private Nds8Key nds8Key;

    public static Nds8 generate(Long reqId, Long declVerId) {
        Nds8 val = new Nds8();

        val.OKV_CODE = val.OPERATION_CODE = val.ACTUAL_ROW_KEY = val.ROW_KEY = val.COMPARE_ROW_KEY = val.CLARIFICATION_KEY =
            val.BROKER_INN = val.BROKER_KPP = val.BUYER_KPP = val.SELLER_KPP = val.SELLER_AGENCY_INFO_KPP =
            val.SELLER_AGENCY_INFO_INN = val.SELLER_AGENCY_INFO_NAME = val.SELLER_AGENCY_INFO_NUM =
            val.CUSTOMS_DECLARATION_NUM = val.RECEIPT_DOC_NUM = val.RECEIPT_DOC_DATE = val.OKV_CODE =
            val.INVOICE_NUM = val.CORRECTION_NUM = val.BUY_ACCEPT_DATE = val.BUYER_INN = val.SELLER_INN =
            val.SELLER_INVOICE_NUM = val.FORMAT_ERRORS = val.LOGICAL_ERRORS = "1234567890";

        val.CHAPTER = val.DEAL_KIND_CODE = val.COMPARE_ALGO_ID = val.IS_DOP_LIST = val.IS_IMPORT = val.IS_CLARIFIED = Integer.MAX_VALUE;

        val.ORDINAL_NUMBER = val.CONTRAGENT_KEY = Long.MAX_VALUE;

        val.CREATE_DATE = val.RECEIVE_DATE = val.INVOICE_DATE = val.CHANGE_DATE = val.CORRECTION_DATE = val.CHANGE_CORRECTION_DATE =
            val.SELLER_INVOICE_DATE = val.SELLER_AGENCY_INFO_DATE = new Date(System.currentTimeMillis());

        val.PRICE_BUY_AMOUNT = val.PRICE_BUY_NDS_AMOUNT = val.PRICE_SELL = val.PRICE_SELL_IN_CURR =
            val.PRICE_SELL_18 = val.PRICE_SELL_10 = val.PRICE_TAX_FREE = val.PRICE_TOTAL = val.PRICE_NDS_TOTAL =
            val.DIFF_CORRECT_DECREASE = val.DIFF_CORRECT_INCREASE = val.DIFF_CORRECT_NDS_DECREASE =
            val.DIFF_CORRECT_NDS_INCREASE = val.PRICE_NDS_BUYER = new BigDecimal(Integer.MAX_VALUE);

        Nds8Key key = new Nds8Key(reqId, declVerId);

        val.REQUEST_ID = reqId;
        val.DECLARATION_VERSION_ID = declVerId;
        val.nds8Key = key;

        return val;
    }

    public Nds8Key getNds8Key() {
        return nds8Key;
    }
}
