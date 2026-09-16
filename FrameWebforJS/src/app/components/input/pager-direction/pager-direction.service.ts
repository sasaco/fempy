import { Injectable } from "@angular/core";
import { Subject } from "rxjs";

@Injectable({
  providedIn: "root",
})
export class PagerDirectionService {
  private pageSelectedSubject = new Subject<number>();

  pageSelected$ = this.pageSelectedSubject.asObservable();

  goToPage(pageNumber: number) {
    this.pageSelectedSubject.next(pageNumber);
  }
}
