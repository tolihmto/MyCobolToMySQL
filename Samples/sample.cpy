      01 CUSTOMER-RECORD.
         05 CUST-ID             PIC 9(9).
         05 CUST-NAME           PIC X(20).
         05 BIRTH-YYYYMMDD      PIC 9(8).
         05 CONTACT-INFO.
            10 PHONE-NUMBER     PIC X(12).
            10 EMAIL            PIC X(25).
         05 BALANCE             PIC S9(9)V9(2) COMP-3.
         05 FLAGS-AREA         REDEFINES BALANCE.
            10 FLAG-A           PIC X(1).
            10 FLAG-B           PIC X(1).
