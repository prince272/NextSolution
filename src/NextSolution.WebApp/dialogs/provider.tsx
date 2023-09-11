"use client";

import { ComponentType, createContext, FC, Fragment, ReactNode, useContext, useEffect, useRef } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import PQueue from "p-queue";
import queryString from "query-string";

import { usePrevious, useStateAsync } from "@/lib/hooks";
import { isEqualSearchParams, sleep } from "@/lib/utils";

import * as components from "./components";

export interface DialogContextProps {
  open: (id: string, props?: any) => Promise<any>;
  close: (id: string) => Promise<any>;
}

export interface DialogProps {
  id: string;
  Component: ComponentType;
  props: any;
  opened: boolean;
  mounted: boolean;
  onClose: (force?: boolean) => void;
}

export const DialogContext = createContext<DialogContextProps>(undefined!);

export interface DialogContextType {}

export const useDialog = () => {
  const context = useContext(DialogContext);
  if (context === undefined) {
    throw new Error("useDialog must be used within DialogProvider");
  }
  return context;
};

const dialogComponents = Object.entries(components).map(([name, Component]) => {
  const id = name.replace(/Modal$/, "").replace(/[A-Z]/g, (char, index) => (index !== 0 ? "-" : "") + char.toLowerCase());
  return { id, name, Component };
});

export const DialogProvider: FC<{ children: ReactNode }> = ({ children }) => {
  const queueRef = useRef(new PQueue({ concurrency: 1 }));

  const [dialogs, setDialogs] = useStateAsync<DialogProps[]>(dialogComponents as any);

  const updateDialog = (id: string, dialog: Partial<DialogProps>) => {
    return setDialogs((prevDialogs) => prevDialogs.map((d) => (d.id === id ? { ...d, ...dialog } : d)));
  };

  const context = useRef<DialogContextProps>({
    open: (id, props) => {
      return queueRef.current.add(async () => {
        await updateDialog(id, {
          ...props,
          opened: true,
          mounted: true
        });
      });
    },
    close: async (id) => {
      return queueRef.current.add(async () => {
        await updateDialog(id, { opened: false, mounted: true });
        await sleep();
        await updateDialog(id, { opened: false, mounted: false });
      });
    }
  });

  return (
    <DialogContext.Provider value={context.current}>
      {children}
      {dialogs.map(({ Component, id, mounted, props, ...dialogProps }) => {
        return mounted ? <Component key={id} {...dialogProps} {...props} /> : <Fragment key={id}></Fragment>;
      })}
    </DialogContext.Provider>
  );
};

export const DIALOG_QUERY_NAME = "dialogId";

export const DialogRouter: FC<{ loading: boolean }> = ({ loading }) => {
  const dialog = useDialog();

  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  const prevPathname = usePrevious(pathname);
  const prevSearchParams = usePrevious(searchParams, (prev, current) => isEqualSearchParams(prev, current));

  const returnHref = useRef("/");

  useEffect(() => {
    (async () => {
      if (loading) return;

      const dialogId = searchParams.get(DIALOG_QUERY_NAME);

      if (dialogId) {
        if (prevPathname && prevSearchParams) {
          const prevDialogId = prevSearchParams.get(DIALOG_QUERY_NAME);
          returnHref.current = prevDialogId ? returnHref.current : queryString.stringifyUrl({ url: prevPathname, query: Object.fromEntries(prevSearchParams) });
        }

        const prevDialogId = prevSearchParams?.get(DIALOG_QUERY_NAME);
        if (prevDialogId) await dialog.close(prevDialogId);

        await dialog.open(dialogId, {
          onClose: async (force?: boolean) => {
            // Close the dialog when it's closed.
            await dialog.close(dialogId);

            if (force) {
              router.replace("/");
            } else {
              router.replace(returnHref.current);
            }
          }
        });
      } else {
        returnHref.current = "/";
      }
    })();
  }, [dialog, loading, pathname, prevPathname, prevSearchParams, router, searchParams]);

  return <></>;
};
