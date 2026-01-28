       01 INVOICE.
          05 INV-ID               PIC 9(8).
          05 ITEM-COUNT           PIC 9(2).
          05 ITEM-LIST OCCURS 0 TO 10 TIMES DEPENDING ON ITEM-COUNT.
             10 PROD-ID           PIC 9(6).
             10 PROD-QTY          PIC 9(4).
             10 PROD-AMT          PIC S9(7)V99.
