/// <reference lib="webworker" />

addEventListener('message', ({ data }) => {

  const fsec = {};
  let error: any = null;

  const jsonData = data.jsonData;
  const member: any[] = data.member;

  // 同じidをもつ部材を探す
  const getMember = (memberNo: string) => {
    const m = member.find((columns) => {
      return columns.id === memberNo;
    })
    if (m === undefined) {
      return { ni: null, nj: null };
    }
    return m;
  }

  // 文字列string を数値にする
  const toNumber = (num: string) => {
    let result: number = null;
    try {
      const tmp: string = num.toString().trim();
      if (tmp.length > 0) {
        result = ((n: number) => isNaN(n) ? null : n)(+tmp);
      }
    } catch {
      result = null;
    }
    return result;
  };


  const max_values = {};
  const value_ranges = {};
  // 断面力の集計
  try {
    for (const caseNo of Object.keys(jsonData)) {
      const max_value = {
        fx: 0, fy: 0, fz: 0,
        mx: 0, my: 0, mz: 0
      };
      const value_range = {
        x: {
          max_d: Number.MIN_VALUE, min_d: Number.MAX_VALUE, max_d_m: '0', min_d_m: '0',
          max_r: Number.MIN_VALUE, min_r: Number.MAX_VALUE, max_r_m: '0', min_r_m: '0',
        },
        y: {
          max_d: Number.MIN_VALUE, min_d: Number.MAX_VALUE, max_d_m: '0', min_d_m: '0',
          max_r: Number.MIN_VALUE, min_r: Number.MAX_VALUE, max_r_m: '0', min_r_m: '0',
        },
        z: {
          max_d: Number.MIN_VALUE, min_d: Number.MAX_VALUE, max_d_m: '0', min_d_m: '0',
          max_r: Number.MIN_VALUE, min_r: Number.MAX_VALUE, max_r_m: '0', min_r_m: '0',
        },
      };

      const target = new Array();
      const caseData: {} = jsonData[caseNo];
      if (typeof (caseData) !== 'object') {
        continue;
      }
      if (!('fsec' in caseData)) {
        continue;
      }
      const json: {} = caseData['fsec'];
      let row = 0;
      let memberNo = '';
      for (const m of Object.keys(json)) {

        let noticePoint = 0.0;
        memberNo = m.replace('member', '');
        const js: {} = json[m];

        let result = {};
        const memb = getMember(memberNo);
        let ni: string = memb.ni;
        let nj = '';
        let counter = 1;
        const data_length: number = Object.keys(js).length;
        while (counter <= data_length) {
          const p = 'P' + counter.toString();
          if (!(p in js)) {
            break;
          }
          const item: {} = js[p];
          let fxi: number = toNumber(item['fxi']);
          let fyi: number = toNumber(item['fyi']);
          let fzi: number = toNumber(item['fzi']);
          let mxi: number = toNumber(item['mxi']);
          let myi: number = toNumber(item['myi']);
          let mzi: number = toNumber(item['mzi']);
          fxi = (fxi == null) ? 0 : Math.round(fxi * 100) / 100;
          fyi = (fyi == null) ? 0 : Math.round(fyi * 100) / 100;
          fzi = (fzi == null) ? 0 : Math.round(fzi * 100) / 100;
          mxi = (mxi == null) ? 0 : Math.round(mxi * 100) / 100;
          myi = (myi == null) ? 0 : Math.round(myi * 100) / 100;
          mzi = (mzi == null) ? 0 : Math.round(mzi * 100) / 100;
          const dummyi = ('dummyi' in item) && item['dummyi'];

          result = {
            m: memberNo,
            n: ni,
            l: noticePoint,
            fx: fxi,
            fy: fyi,
            fz: fzi,
            mx: mxi,
            my: myi,
            mz: mzi,
            dummy: dummyi
          };

          row++;
          result['row'] = row;

          const check_target = (target[target.length - 1] !== undefined) ? target[target.length - 1] :
            { l: '', m: '', n: '', row: '' };
          const check_result = result;
          check_result['row'] -= 1;
          if (check_result['l'] !== check_target.l || check_result['m'] !== check_target.m ||
            check_result['n'] !== check_target.n || check_result['row'] !== check_target.row) {
            target.push(result);
          }

          memberNo = '';
          ni = '';
          if (counter === data_length) {
            nj = memb.nj;
          }

          const l = toNumber(item['L']);
          let fxj: number = toNumber(item['fxj']);
          let fyj: number = toNumber(item['fyj']);
          let fzj: number = toNumber(item['fzj']);
          let mxj: number = toNumber(item['mxj']);
          let myj: number = toNumber(item['myj']);
          let mzj: number = toNumber(item['mzj']);
          noticePoint += Math.round(l * 1000) / 1000;
          fxj = (fxj == null) ? 0 : Math.round(fxj * 100) / 100;
          fyj = (fyj == null) ? 0 : Math.round(fyj * 100) / 100;
          fzj = (fzj == null) ? 0 : Math.round(fzj * 100) / 100;
          mxj = (mxj == null) ? 0 : Math.round(mxj * 100) / 100;
          myj = (myj == null) ? 0 : Math.round(myj * 100) / 100;
          mzj = (mzj == null) ? 0 : Math.round(mzj * 100) / 100;
          const dummyj = ('dummyj' in item) && item['dummyj'];

          result = {
            m: '',
            n: nj,
            l: noticePoint,
            fx: fxj,
            fy: fyj,
            fz: fzj,
            mx: mxj,
            my: myj,
            mz: mzj,
            dummy: dummyj
          };

          row++;
          result['row'] = row;
          target.push(result);
          counter++;

          // 断面力の最大最小とその部材番号を調べる
          if (dummyi === false) {
            // fx //
            value_range.x.max_d = Math.max(fxi, value_range.x.max_d);
            value_range.x.min_d = Math.min(fxi, value_range.x.min_d);
            if (value_range.x.max_d === fxi) value_range.x.max_d_m = m;
            if (value_range.x.min_d === fxi) value_range.x.min_d_m = m;
            // fy //
            value_range.y.max_d = Math.max(fyi, value_range.y.max_d);
            value_range.y.min_d = Math.min(fyi, value_range.y.min_d);
            if (value_range.y.max_d === fyi) value_range.y.max_d_m = m;
            if (value_range.y.min_d === fyi) value_range.y.min_d_m = m;
            // fz //
            value_range.z.max_d = Math.max(fzi, value_range.z.max_d);
            value_range.z.min_d = Math.min(fzi, value_range.z.min_d);
            if (value_range.z.max_d === fzi) value_range.z.max_d_m = m;
            if (value_range.z.min_d === fzi) value_range.z.min_d_m = m;
            // mx //
            value_range.x.max_r = Math.max(mxi, value_range.x.max_r);
            value_range.x.min_r = Math.min(mxi, value_range.x.min_r);
            if (value_range.x.max_r === mxi) value_range.x.max_r_m = m;
            if (value_range.x.min_r === mxi) value_range.x.min_r_m = m;
            // my //
            value_range.y.max_r = Math.max(myi, value_range.y.max_r);
            value_range.y.min_r = Math.min(myi, value_range.y.min_r);
            if (value_range.y.max_r === myi) value_range.y.max_r_m = m;
            if (value_range.y.min_r === myi) value_range.y.min_r_m = m;
            // mz //
            value_range.z.max_r = Math.max(mzi, value_range.z.max_r);
            value_range.z.min_r = Math.min(mzi, value_range.z.min_r);
            if (value_range.z.max_r === mzi) value_range.z.max_r_m = m;
            if (value_range.z.min_r === mzi) value_range.z.min_r_m = m;
          }
          if (dummyj === false) {
            // fx //
            value_range.x.max_d = Math.max(fxj, value_range.x.max_d);
            value_range.x.min_d = Math.min(fxj, value_range.x.min_d);
            if (value_range.x.max_d === fxj) value_range.x.max_d_m = m;
            if (value_range.x.min_d === fxj) value_range.x.min_d_m = m;
            // fy //
            value_range.y.max_d = Math.max(fyj, value_range.y.max_d);
            value_range.y.min_d = Math.min(fyj, value_range.y.min_d);
            if (value_range.y.max_d === fyj) value_range.y.max_d_m = m;
            if (value_range.y.min_d === fyj) value_range.y.min_d_m = m;
            // fz //
            value_range.z.max_d = Math.max(fzj, value_range.z.max_d);
            value_range.z.min_d = Math.min(fzj, value_range.z.min_d);
            if (value_range.z.max_d === fzj) value_range.z.max_d_m = m;
            if (value_range.z.min_d === fzj) value_range.z.min_d_m = m;
            // mx //
            value_range.x.max_r = Math.max(mxj, value_range.x.max_r);
            value_range.x.min_r = Math.min(mxj, value_range.x.min_r);
            if (value_range.x.max_r === mxj) value_range.x.max_r_m = m;
            if (value_range.x.min_r === mxj) value_range.x.min_r_m = m;
            // my //
            value_range.y.max_r = Math.max(myj, value_range.y.max_r);
            value_range.y.min_r = Math.min(myj, value_range.y.min_r);
            if (value_range.y.max_r === myj) value_range.y.max_r_m = m;
            if (value_range.y.min_r === myj) value_range.y.min_r_m = m;
            // mz //
            value_range.z.max_r = Math.max(mzj, value_range.z.max_r);
            value_range.z.min_r = Math.min(mzj, value_range.z.min_r);
            if (value_range.z.max_r === mzj) value_range.z.max_r_m = m;
            if (value_range.z.min_r === mzj) value_range.z.min_r_m = m;
          }
          // max_valueは描画スケールの計算に使用するので自動で設けた着目点も考慮する
          max_value.fx = Math.max(Math.abs(fxi), Math.abs(fxj), max_value.fx);
          max_value.fy = Math.max(Math.abs(fyi), Math.abs(fyj), max_value.fy);
          max_value.fz = Math.max(Math.abs(fzi), Math.abs(fzj), max_value.fz);
          max_value.mx = Math.max(Math.abs(mxi), Math.abs(mxj), max_value.mx);
          max_value.my = Math.max(Math.abs(myi), Math.abs(myj), max_value.my);
          max_value.mz = Math.max(Math.abs(mzi), Math.abs(mzj), max_value.mz);
        }
      }
      const key = caseNo.replace('Case', '');
      fsec[key] = target;
      max_values[key] = max_value;
      value_ranges[key] = value_range;
    }

  } catch (e) {
    error = e;
  }

  postMessage({ fsec, max_values, value_ranges, error });
});
