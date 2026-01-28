       01 ORDER-RECORD.
          05 ORDER-ID             PIC 9(8).
          05 CUST-ID              PIC 9(6).
          05 ALT-GROUP REDEFINES CUST-ID.
             10 CUST-PREFIX       PIC 9(2).
             10 CUST-SUFFIX       PIC 9(4).
          05 PAYMENT.
             10 PAY-TYPE          PIC X(1).
             10 PAY-DETAILS REDEFINES PAY-TYPE.
                15 PAY-CARD       PIC X(1).
