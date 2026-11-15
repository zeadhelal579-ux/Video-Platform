# VideoPlatform — واجهة الديمو

نسخة Static من الواجهة متجهزة لـVercel. وضع الديمو مفعّل افتراضيًا ويشتغل من غير API أو قاعدة بيانات.

## تشغيل وتجربة

```sh
npm ci
npm run dev
```

افتح `http://localhost:5173`، اختار **Try the demo** واضغط **Log in to demo**. بيانات الدخول متعبّية مسبقًا، وهي `demo@videoplatform.example` وكلمة المرور العامة `Demo1234!`.

المكتبة فيها خمس مقاطع من فيديوهات المشروع، مدة كل مقطع 30 ثانية وله ثَمبنيل ووصف عربي وجودتان HLS. المقاطع موجودة في `public/demo/v1` وبتتحمّل عند المشاهدة. الرفع التجريبي يشغّل ملفات MP4 أو WebM لحد 50 MiB، بحد أقصى خمس ملفات لكل تبويب. الملفات المرفوعة مؤقتة وبتختفي بعد Reload.

## النشر على Vercel

اختار **Vite**. لو المستودع فيه المشروع كله، اضبط **Root Directory** على `front Deploy/frontend`. لو المستودع فيه محتويات الحزمة دي فقط، استخدم `./`.

| الإعداد | القيمة |
| --- | --- |
| Build Command | `npm run build` |
| Output Directory | `dist` |
| Environment Variables | الديمو جاهز بالإعدادات الموجودة |

ملف `vercel.json` بيضبط فتح الروابط المباشرة وإعدادات ملفات HLS. لاختبار بناء الإنتاج محليًا، استخدم `npm run build` ثم `npm run preview`. للربط الاختياري بالباك الحقيقي اضبط `VITE_DEMO_MODE=false` و`VITE_API_BASE_URL` قبل البناء.
