# دليل النشر — الباك اند على سيرفر، والفرونت على Vercel

المشروع اتقسم لحتتين مستقلتين:

- **الباك اند** (Api + Worker + PostgreSQL + RabbitMQ + التخزين) بيشتغل على سيرفر بـ Docker Compose — الحزمة دي.
- **الفرونت اند** (React) مشروع لوحده (`videoplatform-frontend`) بيتنشر على **Vercel** كملفات ثابتة.

الدليل ده بيشرح الجزء الأول، وإزاي توصّله بالفرونت (قسم 3 هو الأهم)، وبعدين الإنتاج الحقيقي (S3، backup، scaling).
شرح نشر الفرونت نفسه تفصيلًا موجود في `README.md` بتاع حزمة الفرونت.

> الباك بيستهدف .NET 10 LTS. راجع قسم 9 لحدود التحقق في بيئة النشر.

---

## 0. الصورة الكبيرة

```
                         ┌──────────────────────────┐
   المتصفح ──────────────►  Vercel  (ملفات الفرونت بس) │
      │                  └──────────────────────────┘
      │  (1) طلبات /api   — مباشرة للـ Api، أو عن طريق rewrite بتاع Vercel (قسم 3)
      ├────────────────────────────►  https://api.example.com ──► Caddy ──► Api
      │  (2) رفع الفيديو + تشغيله — مباشرة من/إلى التخزين
      └────────────────────────────►  https://media.example.com ─► Caddy ──► MinIO (أو S3 + CDN)

   على السيرفر (شبكة Docker الداخلية):  Api · Worker(FFmpeg) · PostgreSQL · RabbitMQ · MinIO
```

نقطتين لازم تفهمهم من الأول:

1. **Vercel بيخدم الفرونت بـ HTTPS.** والمتصفح بيرفض (Mixed Content) أي نداء من صفحة `https://`
   لعنوان `http://`. يعني الـ Api **والتخزين** لازم يبقوا على HTTPS. أسهل طريقة: بروفايل `https` (Caddy) في الـ compose.
2. **الـ refresh cookie حساسة لمكان الفرونت بالنسبة للـ Api.** ده سبب إن فيه 3 طرق توصيل مختلفة (قسم 3).

## 1. المتطلبات

- سيرفر Linux (مثلاً Ubuntu 22.04+) بـ **2 vCPU و4GB RAM على الأقل** — الـ FFmpeg
  بياكل CPU كتير — ومساحة قرص على قد الفيديوهات.
- Docker Engine + Docker Compose v2 (اتأكد: `docker compose version`).
- **دومينين على الأقل** بيشاوروا على السيرفر: واحد للـ Api (`api.example.com`) وواحد للتخزين (`media.example.com`)،
  وبورتات **80 و443** مفتوحة (Caddy بيجيب الشهادات تلقائي).
- حساب على Vercel (الخطة المجانية كفاية) ومشروع الفرونت على GitHub.

## 2. تشغيل الباك اند

من جذر الحزمة (الفولدر اللي فيه `docker-compose.yml`):

```bash
cp .env.example .env
nano .env                       # املا الأسرار (قسم 2.1) وعناوين الـ HTTPS
docker compose up -d --build    # أول مرة بياخد كام دقيقة (بيبني image للـ Api وللـ Worker)
docker compose ps               # كل الخدمات لازم تبقى Up / healthy
curl https://api.example.com/api/health          # {"status":"ok"}
curl https://api.example.com/api/health/ready    # {"status":"ready"}  = الداتابيز متصلة
```

### 2.1 ملف `.env`

انسخ `.env.example` لـ `.env` (الملف ده **ماتعملوش commit**). كل متغير موثق جواه. الأهم:

| المتغير | معناه |
|---|---|
| `POSTGRES_PASSWORD`, `RABBITMQ_PASSWORD` | باسوردات قاعدة البيانات والـ broker. **حروف وأرقام بس**. ولّد: `openssl rand -hex 24` |
| `JWT_SIGNING_KEY` | سر توقيع التوكنز، **32 حرف على الأقل**. ولّد: `openssl rand -hex 32`. الـ Api **مش هيشتغل** بقيمة فاضية أو placeholder |
| `S3_ACCESS_KEY`, `S3_SECRET_KEY` | مع MinIO المدمج بيبقوا اليوزر والباسورد (3+ و8+ حروف). مع AWS بيبقوا مفاتيح الـ IAM user |
| `COMPOSE_PROFILES` | `local-storage,https` = MinIO + Caddy (HTTPS). سيب `local-storage` بره لو هتستخدم S3 |
| `API_DOMAIN`, `MEDIA_DOMAIN` | دومين الـ Api ودومين التخزين (مع بروفايل `https`) |
| `S3_ENDPOINT_PUBLIC` | عنوان التخزين **من وجهة نظر المتصفح** — لازم `https://` = `https://media.example.com` |
| `MEDIA_BASE_URL` | العنوان اللي المشغّل بيحمّل منه الفيديو = `S3_ENDPOINT_PUBLIC` + `/videoplatform` |
| `APP_URL` | عنوان الفرونت على Vercel بالظبط (مثلاً `https://myapp.vercel.app`) — ده الـ CORS allow-list (قسم 3) |
| `AUTH_COOKIE_SAMESITE` | `Strict` (افتراضي) أو `None` — حسب الطريقة اللي هتختارها في قسم 3 |
| `AUTH_RATE_LIMIT_PER_MINUTE` | محاولات login/register في الدقيقة لكل IP (افتراضي 20) |

مثال كامل (طريقة B في قسم 3):

```
COMPOSE_PROFILES=local-storage,https
API_DOMAIN=api.example.com
MEDIA_DOMAIN=media.example.com
S3_ENDPOINT_PUBLIC=https://media.example.com
MEDIA_BASE_URL=https://media.example.com/videoplatform
APP_URL=https://app.example.com
AUTH_COOKIE_SAMESITE=Strict
```

### 2.2 الـ Ports

| Port | مين بيستخدمه | ينفع يتفتح للإنترنت؟ |
|---|---|---|
| `80`, `443` | Caddy (الـ Api + التخزين بـ HTTPS) | أيوه |
| `9000` | MinIO مباشرة | **لأ** بعد ما HTTPS يشتغل — اقفله من الـ firewall (Caddy بيوصل له من جوه Docker) |
| `127.0.0.1:8080` | الـ Api (loopback بس — لو هتستخدم Tunnel على نفس الجهاز) | لأ — محلي بس |
| `127.0.0.1:15672`, `127.0.0.1:9001` | واجهات RabbitMQ وMinIO | لأ — محلي بس (SSH tunnel) |
| Postgres / RabbitMQ | شبكة Docker الداخلية بس | **متتفتحش** |

> الـ Api بيثق في الـ headers اللي بتيجي من الـ proxy بتاعنا (`X-Forwarded-*`)،
> عشان كده **ماتنشرش بورت الـ Api على الإنترنت مباشرة** — لازم Caddy (أو Tunnel) قدامه.

## 3. إزاي المتصفح يوصل للـ Api؟ (اختار طريقة واحدة)

الفرونت بيحتاج يعرف الـ Api فين. فيه 3 طرق، والاختيار بيتحدد بحاجة واحدة: **هل الفرونت والـ Api على نفس الـ "site"؟**
(الـ site = الدومين الأساسي. `app.example.com` و`api.example.com` نفس الـ site. لكن `myapp.vercel.app` و`example.com` مختلفين.)

| | A — Rewrite على Vercel | B — مباشر + دومين خاص | C — مباشر + دومين مختلف |
|---|---|---|---|
| مناسب لـ | عندك `*.vercel.app` بس | عندك دومين وبتستخدم `app.` و`api.` | الفرونت `*.vercel.app` والـ Api على دومين تاني، من غير rewrite |
| الفرونت (Vercel) | غيّر host الـ rewrite في `vercel.json`، و`VITE_API_BASE_URL` فاضي | `VITE_API_BASE_URL=https://api.example.com` | `VITE_API_BASE_URL=https://api.other.com` |
| `AUTH_COOKIE_SAMESITE` | `Strict` | `Strict` | **`None`** |
| `APP_URL` (CORS) | مش مستخدم | `https://app.example.com` | `https://myapp.vercel.app` |
| Safari / iOS | شغال ✅ | شغال ✅ | **الجلسة بتضيع مع كل reload** ❌ |
| Preview deployments | شغالة ✅ | محتاجة تضيف عنوانها في `APP_URL` | محتاجة تضيف عنوانها في `APP_URL` |
| الـ rate limit | مشترك بين كل الزوار ⚠️ ارفعه (120) | لكل زائر ✅ | لكل زائر ✅ |
| التوصية | الأسهل للتجربة والمشاريع الصغيرة | **الأفضل للإنتاج** | آخر اختيار |

### ليه الاختيار مهم؟ (الشرح)

التوكن اللي بيحافظ على تسجيل الدخول بعد الـ reload هو **cookie اسمها `refreshToken`** (httpOnly، مش بتتقري من الـ JavaScript).
الـ Api كان بيحطها بـ `SameSite=Strict`، يعني المتصفح **مش هيبعتها ولا هيخزنها** لو الطلب جاي من site مختلف.
- في **A** المتصفح شايف origin واحد (Vercel بيعمل proxy لـ `/api`) → مفيش مشكلة.
- في **B** `app.example.com` و`api.example.com` نفس الـ site → `Strict` شغال.
- في **C** الـ sites مختلفة → لازم `SameSite=None` (مع `Secure`)، والـ Api وقتها بيطلب كمان header اسمه `X-Requested-With`
  على `/refresh` و`/logout` (الفرونت بيبعته دايمًا) عشان صفحة خبيثة ماتقدرش تستعمل الـ cookie. وSafari بيمنع الـ cookies دي تمامًا.

الكود اتجرب ضد الحالتين في متصفح حقيقي (Chromium): مع `Strict` والدومينين مختلفين الجلسة بتضيع عند الـ reload، ومع `None` بتفضل.

### الطريقة A بالتفصيل (Rewrite)

1. في مشروع الفرونت افتح `vercel.json` وغيّر:
   ```json
   "destination": "https://REPLACE-WITH-YOUR-API-HOST.invalid/api/:path*"
   ```
   لـ `"https://api.example.com/api/:path*"` (دومين الـ Api عندك — **لازم https**).
2. سيب `VITE_API_BASE_URL` فاضي.
3. في `.env` بتاع الباك: `AUTH_COOKIE_SAMESITE=Strict` و`AUTH_RATE_LIMIT_PER_MINUTE=120`.

> في الطريقة دي كل الزوار بيوصلوا للـ Api من سيرفرات Vercel، فالـ rate limit (حسب الـ IP) بيبقى مشترك. عشان كده رفعناه.
> حماية كل حساب لوحده (Account lockout بعد محاولات غلط) فاضلة شغالة.

### الطريقة B بالتفصيل (مباشر + دومين خاص)

1. على Vercel: Settings → Domains → ضيف `app.example.com`. وعلى السيرفر DNS: `api.example.com` و`media.example.com`.
2. متغير بيئة على Vercel: `VITE_API_BASE_URL=https://api.example.com` (من غير `/api` في الآخر ومن غير `/`).
3. في `.env` بتاع الباك: `APP_URL=https://app.example.com` و`AUTH_COOKIE_SAMESITE=Strict`.

لو عايز أكتر من عنوان (الدومين + عنوان الـ `*.vercel.app`): `APP_URL=https://app.example.com,https://myapp.vercel.app`.
**ماينفعش wildcard** (`*.vercel.app`) عن قصد — ده كان هيسمح لأي موقع على Vercel يتصرف باسم المستخدم المسجّل.

### الطريقة C بالتفصيل (مباشر + cross-site)

نفس B لكن `AUTH_COOKIE_SAMESITE=None` و`APP_URL=https://myapp.vercel.app`. لازم HTTPS على الاتنين.
متوقع إن مستخدمين Safari/iPhone هيلاقوا نفسهم مسجلين خروج مع كل reload.

## 4. نشر الفرونت على Vercel (ملخص)

التفاصيل الكاملة في `README.md` بتاع حزمة الفرونت. باختصار:

1. ارفع حزمة الفرونت على repo GitHub (جذر الـ repo = الفولدر اللي فيه `package.json` و`vercel.json`).
2. شغّل `npm install` مرة على جهازك وارفع `package-lock.json` (بيخلي الـ build ثابت).
3. Vercel → Add New → Project → اختار الـ repo. الـ Framework هيتعرف تلقائي (Vite). متغيّرش حاجة في الـ Build settings.
4. Environment Variables: ضيف `VITE_API_BASE_URL` (طريقة B/C بس).
5. Deploy. لو الإعدادات ناقصة الـ build **هيقف برسالة واضحة** (`DEPLOY BLOCKED`) بدل ما ينشر موقع مكسور.
6. خد عنوان الموقع وحطه في `APP_URL` في `.env` بتاع الباك (B/C)، وبعدين: `docker compose up -d` (بيعيد تشغيل الـ Api بالقيمة الجديدة).

## 5. التخزين والـ CORS

- **MinIO المدمج:** بيرد على الـ CORS لكل الـ origins افتراضيًا، فالرفع والتشغيل من Vercel بيشتغلوا من غير إعداد إضافي — بشرط HTTPS (بروفايل `https`).
- **AWS S3 (موصى بيه للإنتاج):**
  1. اعمل bucket، وIAM user بالصلاحية دي بس (غيّر اسم الـ bucket):
     ```json
     { "Version": "2012-10-17",
       "Statement": [{ "Effect": "Allow", "Action": ["s3:GetObject", "s3:PutObject"],
                       "Resource": "arn:aws:s3:::my-video-bucket/*" }] }
     ```
  2. افتح `deploy/s3-cors.json` وغيّر `AllowedOrigins` لعنوان الفرونت على Vercel (ممكن أكتر من عنوان)، وبعدين:
     ```bash
     aws s3api put-bucket-cors --bucket my-video-bucket --cors-configuration file://deploy/s3-cors.json
     ```
  3. حط CDN (CloudFront أو Cloudflare) قدام الـ bucket، واديله قراءة على `videos/*/hls/*` بس (ملفات الرفع الأصلية تفضل خاصة).
  4. في `.env`: `COMPOSE_PROFILES=https` (من غير `local-storage`) + `S3_BUCKET`, `S3_REGION`, `S3_FORCE_PATH_STYLE=false`،
     وسيب `S3_ENDPOINT_INTERNAL` و`S3_ENDPOINT_PUBLIC` فاضيين، و`MEDIA_BASE_URL=https://cdn.example.com`.
- **Cloudflare R2 / أي S3-compatible:** حط الـ endpoint في `S3_ENDPOINT_INTERNAL` و`S3_ENDPOINT_PUBLIC` (نفس القيمة) واستخدم `S3_REGION=auto` (لـ R2).

## 6. التأكد إن كل حاجة شغالة

على الباك:
```bash
curl https://api.example.com/api/health          # {"status":"ok"}
docker compose logs -f api worker                # اللوجز الحية
```
- واجهة RabbitMQ: محلية (`127.0.0.1:15672`). من جهازك: `ssh -L 15672:localhost:15672 user@السيرفر` ثم `http://localhost:15672`.
- الرسايل اللي فشلت بعد كل المحاولات بتترمي في طابور `video-processing_error` وبتقدر تعيد إرسالها من الواجهة.

على الموقع (من Vercel):
1. اعمل حساب وسجّل دخول.
2. **اعمل Reload** — لازم تفضل مسجّل (ده اختبار الـ cookie؛ لو رجعك لصفحة login راجع قسم 3 وقسم 11).
3. ارفع فيديو صغير (MP4) — الحالة بتمشي `Queued` ← `Processing` ← `Ready` والمشغّل بيظهر.
4. افتح DevTools → Network: مفيش أخطاء CORS ولا Mixed Content.

## 7. قاعدة البيانات والـ migrations

- الـ Api بيعمل الجداول لوحده عند أول تشغيل (`EnsureCreated`) — كفاية لأول نشر.
- **قبل أول تعديل في شكل الجداول** لازم تولّد migrations حقيقية (على جهازك):
  ```bash
  dotnet tool install --global dotnet-ef --version 10.0.15
  cd back/src/VideoPlatform.Infrastructure
  dotnet ef migrations add InitialCreate --startup-project ../VideoPlatform.Api --output-dir Persistence/Migrations
  ```
  اعمل commit للملفات؛ بعدها الـ Api بيطبقهم تلقائيًا.
- **ماتخلطش الطريقتين على نفس الداتابيز:** داتابيز اتعملت بـ `EnsureCreated` مفيهاش
  تاريخ migrations، فتطبيق `InitialCreate` عليها هيفشل. الأحسن تولّد الـ migrations
  **قبل أول نشر**. خُد نسخة احتياطية وخطّط لنقل البيانات لو القاعدة الحالية اتعملت بـ `EnsureCreated`.

## 8. التوسع، الـ Backup، التحديث

```bash
docker compose up -d --scale worker=3     # 3 Workers بيسحبوا من نفس الطابور
```
كل Worker بيعالج `WORKER_MAX_CONCURRENT_JOBS` فيديو في نفس الوقت (الافتراضي 1، وده الصح لأن FFmpeg بيستخدم كل الـ cores).
الـ Api stateless ويتكرر بنفس الطريقة (الـ rate limit بيتحسب لكل نسخة على حدة).

```bash
# Backup لقاعدة البيانات
docker compose exec -T postgres pg_dump -U videoplatform videoplatform > backup.sql
# استرجاع (على داتابيز فاضية)
docker compose exec -T postgres psql -U videoplatform videoplatform < backup.sql

# تحديث
git pull && docker compose up -d --build && docker compose logs -f api worker
```
الفيديوهات نفسها: خد backup من volume `minio-data` (أو فعّل versioning/replication لو S3).

## 9. حاجات لازم تعرفها (قرارات وقيود)

1. **.NET 10 LTS:** المشاريع بتستهدف `net10.0`، وصور API وWorker بتستخدم وسم `10.0`. صور Linux الافتراضية مبنية على Ubuntu. استخدم SDK 10 للتطوير المحلي. [سياسة دعم .NET](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) · [تغيير صور .NET 10](https://learn.microsoft.com/en-us/dotnet/core/compatibility/containers/10.0/default-images-use-ubuntu).
2. **MinIO:** نسخة المجتمع المجانية مبقتش بتتصان من الشركة (المستودع read-only ومفيش
   images جديدة). كويس لتجربة أو سيرفر واحد؛ للإنتاج استخدم S3 أو R2 (قسم 5).
3. **RabbitMQ** متثبت على `3.13` عشان متوافق مع MassTransit 8.3 المستخدم. ماترقّيهوش لـ 4.x من غير ما تجرب.
4. **Vercel Preview deployments:** كل preview ليه عنوان مختلف. مع الطريقة A بتشتغل عادي. مع B/C لازم تضيف عنوان الـ preview في `APP_URL`.
5. **تحقق الكود:** بناء الحل واختباراته نجحوا على .NET 10 محليًا. بناء صور Docker وتشغيل Compose يحتاجوا تحقق في بيئة Docker.

## 10. قايمة الأمان قبل الإطلاق

- [ ] كل الأسرار في `.env` قيم عشوائية جديدة، و`.env` مش في git.
- [ ] HTTPS شغال على الـ Api **وعلى** عنوان الـ media.
- [ ] مفيش بورت غير 80/443 مفتوح للإنترنت (9000 مقفول بعد HTTPS).
- [ ] واجهة RabbitMQ وconsole بتاع MinIO متاحين من داخل السيرفر بس.
- [ ] `APP_URL` فيه عنوان الفرونت الفعلي بس (مفيش wildcard).
- [ ] رفعت فيديو تجريبي من موقع Vercel لحد `Ready` وشغلته، وعملت reload وفضلت مسجّل.
- [ ] جدولت backup للداتابيز.

## 11. حل المشاكل الشائعة

| العَرَض | السبب الغالب |
|---|---|
| الـ build على Vercel بيقف برسالة `DEPLOY BLOCKED` | `VITE_API_BASE_URL` فاضي و`vercel.json` لسه فيه الـ placeholder — اتبع قسم 3 |
| تسجيل الدخول بيقول "The server sent an unexpected response" | طلبات `/api` مش بتوصل للـ Api: الـ rewrite غلط، أو `VITE_API_BASE_URL` غلط (بعد تغييره لازم Redeploy) |
| بعد الـ reload بيرجعني لصفحة login | الـ cookie مش بتتخزن: دومينات مختلفة مع `Strict` → استخدم A أو B، أو C مع `AUTH_COOKIE_SAMESITE=None`. على Safari/iOS الطريقة C مش هتنفع |
| خطأ CORS في الـ console | `APP_URL` مش مطابق للعنوان اللي في شريط المتصفح (`https`، نفس الدومين، من غير مسار) — وبعد تغييره `docker compose up -d` |
| "Mixed Content" في الـ console | `VITE_API_BASE_URL` أو `S3_ENDPOINT_PUBLIC` أو `MEDIA_BASE_URL` بـ `http://` — لازم `https://` |
| الرفع بيفشل مع CORS | مع S3: طبّق `deploy/s3-cors.json` على الـ bucket بعنوان موقع Vercel الصح |
| الفيديو `Ready` بس المشغّل بيقول network error | CORS على عنوان الـ media/CDN، أو `MEDIA_BASE_URL` غلط |
| الفيديو `Ready` والصفحة بتقول "Playback isn't available" | `MEDIA_BASE_URL` (Storage:PublicBaseUrl) فاضي في الـ Api |
| الفيديو `Failed` | `docker compose logs worker` — غالبًا ملف تالف أو FFmpeg. الرسالة الكاملة في `ProcessingJobs.ErrorMessage` |
| الفيديو واقف على `Queued` | الـ Worker واقف أو RabbitMQ: `docker compose ps` وشوف طابور `video-processing` في الواجهة |
| الـ Api بيقف عند التشغيل برسالة `Jwt:SigningKey` | `JWT_SIGNING_KEY` فاضي أو أقل من 32 حرف أو لسه placeholder |
| `password authentication failed` | باسورد Postgres اتغير بعد أول تشغيل — الـ volume القديم لسه محتفظ بالقديم: `docker compose down -v` (بيمسح البيانات!) |
| `429 Too many attempts` | حد المحاولات على login/register — استنى دقيقة، أو ارفع `AUTH_RATE_LIMIT_PER_MINUTE` (خصوصًا مع الطريقة A) |
| صفحة فيها خطأ بعد نشر نسخة جديدة وإنت فاتح التاب | الملفات القديمة اتمسحت — الموقع بيعمل reload تلقائي مرة واحدة؛ لو فضل، اعمل Ctrl+F5 |
