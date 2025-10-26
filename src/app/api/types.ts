export interface Book {
  id: number;
  title: string;
  author: string;
  category?: string | null;
    // لو السيرفر يرسلها مباشرة
  availableCopies?: number | null;
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
// report.types.ts

export interface DashboardStats {
  totalBooks: number;
  totalMembers: number;
  borrowedBooks: number;
  overdueBooks: number;
}

export interface MostBorrowedRow {
  bookId: number;
  title: string;
  borrowCount: number;
}

export interface OverdueRow {
  id: number;
  bookTitle: string;
  memberName: string;
  memberEmail: string;
  memberPhone: string;
  dueDate: string;   // ISO
  daysLate: number;
}

export interface ActiveMemberRow {
  memberId: number;
  name: string;
  totalBorrowings: number;
}

export interface BorrowRecordsQuery {
  memberId?: number;
  bookId?: number;
  fromDate?: string; // ISO yyyy-MM-dd
  toDate?: string;   // ISO yyyy-MM-dd
}
export interface DashboardStats {
  totalBooks: number;
  totalMembers: number;
  borrowedBooks: number;
  overdueBooks: number;
}

export interface MostBorrowedRow {
  bookId: number;
  title: string;
  borrowCount: number;
}

export interface OverdueRow {
  id: number;
  bookTitle: string;
  memberName: string;
  memberEmail: string;
  memberPhone: string;
  dueDate: string;   // ISO
  daysLate: number;
}


export interface BorrowRecordsQuery {
  memberId?: number;
  bookId?: number;
  fromDate?: string; // yyyy-MM-dd
  toDate?: string;   // yyyy-MM-dd
}

export interface BorrowRecordExportRow {
  id: number;
  memberId: number;
  memberName: string;
  bookId: number;
  bookTitle: string;
  borrowedDate: string;        // ISO
  dueDate: string;             // ISO
  returnedDate?: string | null;// ISO or null
  status: string;
  overdueDays: number;
}

/** جديد */
export type TrendBucket = 'day' | 'week' | 'month';

export interface BorrowTrendPoint {
  date: string;   // yyyy-MM-dd (مفتاح التجميع)
  borrow: number;
  return: number; // حقل @return في الباك صار 'return' هنا
}

export interface CategoryStat {
  category: string;
  count: number;
  share: number; // 0..1
}

export interface Cohorts {
  veryActive: number;
  active: number;
  light: number;
  inactive: number;
  totalMembers: number;
  days: number;
}

export interface BorrowRecordExportRow {
  id: number;
  memberId: number;
  memberName: string;
  bookId: number;
  bookTitle: string;
  borrowedDate: string;        // ISO
  dueDate: string;             // ISO
  returnedDate?: string | null;// ISO
  status: string;              // "تم الإرجاع" أو "مستعار"
  overdueDays: number;
}

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
