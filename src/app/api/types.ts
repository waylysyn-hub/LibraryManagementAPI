export interface Book {
  id: number;
  title: string;
  author: string;
  category?: string | null;
  year?: number | null;
  copiesCount?: number | null;
  isbn?: string | null;
  borrowCount?: number | true; // يظهر لو IncludeBorrowCount = true
  coverUrl?: string; // اختياري
}


export interface PagedResult<T> {
  success: boolean;
  message: string;
  data: T[];
  items: T[];
  total: number;
  meta: {
    page: number;
    pageSize: number;
    total: number;
    totalPages: number;
    sortBy: string;
    sortDir: string;
  };
}

export type SortDir = 'Asc' | 'Desc';

export interface BookQuery {
  Q?: string;
  Title?: string;
  Author?: string;
  Category?: string;
  Isbn?: string;
  IsbnStartsWith?: boolean;
  YearFrom?: number;
  YearTo?: number;
  MinCopies?: number;
  MaxCopies?: number;
  SortBy?: keyof Book | '--';
  SortDir?: SortDir | '--';
  Page?: number;
  PageSize?: number;
  IncludeBorrowCount?: boolean;
}
