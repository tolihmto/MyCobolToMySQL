       01 POLICY.
          05 HEADER.
             10 FILLER            PIC X(2).
             10 COMPANY-CODE      PIC X(3).
             10 FILLER            PIC X(1).
          05 HOLDER.
             10 HOLDER-ID         PIC 9(8).
             10 HOLDER-NAME.
                15 LAST-NAME      PIC X(20).
                15 FIRST-NAME     PIC X(15).
          05 DETAILS.
             10 START-DATE        PIC 9(8).
             10 END-DATE          PIC 9(8).
