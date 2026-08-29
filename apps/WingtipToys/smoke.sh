#!/usr/bin/env bash
# The shopper's journey against a running WingtipToys.Host: register, log off, log
# back in, browse the catalog over Friendly URLs and the two MapPageRoute routes,
# add to the cart, change a quantity, watch the master page's cart count follow,
# and start checkout as far as the PayPal boundary. bash + curl only, and no bash-4
# builtins, so the same script runs on macOS, Linux, and Git bash on Windows.
#
#   apps/WingtipToys/smoke.sh [base-url]

set -uo pipefail

BASE="${1:-http://127.0.0.1:5085}"
EMAIL="shopper+$(date +%s)@example.com"
PASSWORD='Passw0rd!'
# ScriptManager selects its script set from the browser capabilities, and the
# account pages' unobtrusive validators ride on that choice.
UA='Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
jar="$work/cookies.txt"
: > "$jar"

failures=0

pass() { printf 'PASS  %s\n' "$1"; }
fail() { printf 'FAIL  %s\n        %s\n' "$1" "$2"; failures=$((failures + 1)); }

check() { # check <label> <actual> <expected>
  if [ "$2" = "$3" ]; then pass "$1"; else fail "$1" "expected: $3
        actual:   $2"; fi
}

check_contains() { # check_contains <label> <file> <needle>
  if grep -qF -- "$3" "$2"; then pass "$1"; else fail "$1" "missing: $3"; fi
}

location() { grep -i '^location:' "$1" | tr -d '\r' | sed 's/^[Ll]ocation: *//'; }

field() { # field <html> <name>
  tr '<' '\n' < "$1" | grep "name=\"$2\"" | sed 's/.*value="//; s/".*//' | head -1
}

control_names() { # control_names <html> <id-suffix> -- generated names, one per row
  tr '<' '\n' < "$1" | grep -o "name=\"[^\"]*$2\"" | sed 's/name="//; s/"$//'
}

submit_name() { # the auto-generated submit button name ASP.NET assigns the form
  tr '<' '\n' < "$1" | grep 'type="submit"' | grep -o 'name="[^"]*"' | head -1 |
    sed 's/name="//; s/"$//'
}

row_quantity_name() { # row_quantity_name <html> <product-name> -- the GridView orders
                      # its rows by whatever the query returns, so pick by product
  awk -v want="$2" 'BEGIN { RS = "<tr" }
    index($0, want) > 0 && match($0, /name="[^"]*PurchaseQuantity"/) {
      print substr($0, RSTART + 6, RLENGTH - 7); exit }' "$1"
}

money_digits() { # money_digits <html> <id-suffix> -- a currency label's digits only,
                 # because the host machine's culture picks the symbol and separators
  grep -o "id=\"[^\"]*$2\">[^<]*" "$1" | head -1 | tr -cd '0-9'
}

postback_target() { # the LoginStatus __doPostBack target rendered by Site.Master
  grep -o "__doPostBack(&#39;[^&]*&#39;" "$1" | head -1 |
    sed 's/__doPostBack(&#39;//; s/&#39;$//'
}

post_url() { # post_url <html> <request-path> -- the rendered action, resolved
  local action folder
  action=$(sed -n 's/.*<form[^>]*action="\([^"]*\)".*/\1/p' "$1" | head -1)
  folder=${2%/*}
  printf '%s%s/%s' "$BASE" "$folder" "${action#./}"
}

get() { # get <path> <name>
  : > "$work/$2.html"
  : > "$work/$2.head"
  curl -sS -A "$UA" -b "$jar" -c "$jar" -o "$work/$2.html" -D "$work/$2.head" \
    -w '%{http_code}' --max-time 300 "$BASE$1"
}

# The three hidden postback fields are read into globals rather than an array:
# macOS still ships bash 3.2, which has neither mapfile nor safe array round-trips.
VS=''; VSGEN=''; EVAL=''
read_postback_fields() {
  VS=$(field "$1" __VIEWSTATE)
  VSGEN=$(field "$1" __VIEWSTATEGENERATOR)
  EVAL=$(field "$1" __EVENTVALIDATION)
}

post() { # post <url> <name> [curl args...] -- carries the fields read above
  local url=$1 name=$2
  shift 2
  : > "$work/$name.html"
  : > "$work/$name.head"
  curl -sS -A "$UA" -b "$jar" -c "$jar" -o "$work/$name.html" -D "$work/$name.head" \
    -w '%{http_code}' --max-time 300 -X POST \
    --data-urlencode "__VIEWSTATE=$VS" \
    --data-urlencode "__VIEWSTATEGENERATOR=$VSGEN" \
    --data-urlencode "__EVENTVALIDATION=$EVAL" \
    "$@" "$url"
}

echo "base $BASE"
echo "user $EMAIL"
echo

# --- the anonymous site: master page, category menu, folder authorization --------

status=$(get / home-anon)
check 'GET / anonymous -> 200' "$status" 200
check_contains 'GET / renders the Wingtip Toys home page' "$work/home-anon.html" \
  'Wingtip Toys can help you find the perfect gift.'
check_contains 'GET / sets __AntiXsrfToken' "$work/home-anon.head" '__AntiXsrfToken='
check_contains 'the master page seeds its category menu from the database' \
  "$work/home-anon.html" '/Category/Cars'
check_contains 'the master page reports an empty cart' "$work/home-anon.html" 'Cart (0)'

status=$(get /Checkout/CheckoutStart.aspx checkout-anon)
check 'GET /Checkout anonymous -> 302 (folder <deny users="?">)' "$status" 302
check 'GET /Checkout anonymous -> login Location' \
  "$(location "$work/checkout-anon.head")" \
  "$BASE/Account/Login?ReturnUrl=%2FCheckout%2FCheckoutStart.aspx"

status=$(get /Admin/AdminPage admin-anon)
check 'GET /Admin anonymous -> 302 (folder <allow roles="canEdit">)' "$status" 302

# --- register, log off, log back in ----------------------------------------------

status=$(get /Account/Register register)
check 'GET /Account/Register -> 200' "$status" 200
check_contains 'the register page renders its validators' "$work/register.html" \
  'The email field is required.'

read_postback_fields "$work/register.html"
status=$(post "$(post_url "$work/register.html" /Account/Register)" register-post \
  --data-urlencode "ctl00\$MainContent\$Email=$EMAIL" \
  --data-urlencode "ctl00\$MainContent\$Password=$PASSWORD" \
  --data-urlencode "ctl00\$MainContent\$ConfirmPassword=$PASSWORD" \
  --data-urlencode "$(submit_name "$work/register.html")=Register")
check 'POST /Account/Register -> 302' "$status" 302
check 'POST /Account/Register -> Location /' "$(location "$work/register-post.head")" /
check_contains 'register sets .AspNet.ApplicationCookie' "$work/register-post.head" \
  'Set-Cookie: .AspNet.ApplicationCookie='

status=$(get / home-registered)
check 'GET / after register -> 200' "$status" 200
check_contains 'the master page greets the registered user' "$work/home-registered.html" \
  "Hello, $EMAIL !"

read_postback_fields "$work/home-registered.html"
status=$(post "$(post_url "$work/home-registered.html" /)" logoff \
  --data-urlencode "__EVENTTARGET=$(postback_target "$work/home-registered.html")" \
  --data-urlencode '__EVENTARGUMENT=')
check 'POST / log off -> 302' "$status" 302
check_contains 'log off expires .AspNet.ApplicationCookie' "$work/logoff.head" \
  'Set-Cookie: .AspNet.ApplicationCookie=; expires='

status=$(get /Account/Login login)
check 'GET /Account/Login -> 200' "$status" 200
read_postback_fields "$work/login.html"
status=$(post "$(post_url "$work/login.html" /Account/Login)" login-post \
  --data-urlencode "ctl00\$MainContent\$Email=$EMAIL" \
  --data-urlencode "ctl00\$MainContent\$Password=$PASSWORD" \
  --data-urlencode "$(submit_name "$work/login.html")=Log in")
check 'POST /Account/Login -> 302' "$status" 302
check 'POST /Account/Login -> Location /' "$(location "$work/login-post.head")" /
check_contains 'login sets .AspNet.ApplicationCookie' "$work/login-post.head" \
  'Set-Cookie: .AspNet.ApplicationCookie='

# --- browse: Friendly URLs, both MapPageRoute routes, model binding --------------

status=$(get /ProductList products)
check 'GET /ProductList -> 200 (Friendly URL)' "$status" 200
check_contains 'the product ListView binds the catalog' "$work/products.html" \
  'Convertible Car'
check_contains 'the product list links details through GetRouteUrl' "$work/products.html" \
  '/Product/Convertible%20Car'

status=$(get /Category/Rockets category)
check 'GET /Category/Rockets -> 200 (MapPageRoute + [RouteData])' "$status" 200
check_contains 'the category route filters the list to its category' \
  "$work/category.html" 'Rocket'

status=$(get /Product/Convertible%20Car details)
check 'GET /Product/Convertible Car -> 200 (MapPageRoute + FormView)' "$status" 200
check_contains 'the details FormView binds the product description' "$work/details.html" \
  'neutrino based battery'
check_contains 'the details FormView binds the product number' "$work/details.html" \
  'Product Number:'

# --- cart: add, view, update quantity, master-page count ------------------------

status=$(get "/AddToCart.aspx?productID=1" add-extension)
check 'GET /AddToCart.aspx -> 301 (Friendly URLs, RedirectMode.Permanent)' "$status" 301
check 'the permanent redirect drops the extension' \
  "$(location "$work/add-extension.head")" '/AddToCart?productID=1'

status=$(get "/AddToCart?productID=1" add1)
check 'GET /AddToCart?productID=1 -> 302' "$status" 302
check 'AddToCart redirects to the cart' "$(location "$work/add1.head")" '/ShoppingCart.aspx'

status=$(get "/AddToCart?productID=16" add2)
check 'GET /AddToCart?productID=16 -> 302' "$status" 302

status=$(get /ShoppingCart cart)
check 'GET /ShoppingCart -> 200' "$status" 200
check_contains 'the cart GridView lists the first product' "$work/cart.html" 'Convertible Car'
check_contains 'the cart GridView lists the second product' "$work/cart.html" 'Rocket'
check 'the cart totals both lines' "$(money_digits "$work/cart.html" lblTotal)" 14545
check_contains 'the master page cart count follows the cart' "$work/cart.html" 'Cart (2)'

car=$(row_quantity_name "$work/cart.html" 'Convertible Car')
rocket=$(row_quantity_name "$work/cart.html" 'Rocket')
if [ -n "$car" ] && [ -n "$rocket" ] && [ "$car" != "$rocket" ]; then
  pass 'the cart renders a quantity box per row'
else
  fail 'the cart renders a quantity box per row' "found: [$car] [$rocket]"
fi
read_postback_fields "$work/cart.html"
status=$(post "$(post_url "$work/cart.html" /ShoppingCart)" cart-update \
  --data-urlencode "$car=3" \
  --data-urlencode "$rocket=1" \
  --data-urlencode "$(control_names "$work/cart.html" 'UpdateBtn' | sed -n 1p)=Update")
check 'POST /ShoppingCart Update -> 200' "$status" 200
check 'the Update postback rebinds the new total' \
  "$(money_digits "$work/cart-update.html" lblTotal)" 19045

status=$(get /ShoppingCart cart-after)
check 'GET /ShoppingCart after Update -> 200' "$status" 200
check 'the quantity change survives into a fresh request' \
  "$(money_digits "$work/cart-after.html" lblTotal)" 19045
check_contains 'the master page cart count follows the new quantity' \
  "$work/cart-after.html" 'Cart (4)'

# --- checkout, as far as the PayPal boundary ------------------------------------

checkout=$(control_names "$work/cart-after.html" 'CheckoutImageBtn' | sed -n 1p)
read_postback_fields "$work/cart-after.html"
status=$(post "$(post_url "$work/cart-after.html" /ShoppingCart)" checkout-post \
  --data-urlencode "$checkout.x=72" --data-urlencode "$checkout.y=11")
check 'POST /ShoppingCart Check out -> 302' "$status" 302
check 'Check out redirects into the checkout folder' \
  "$(location "$work/checkout-post.head")" '/Checkout/CheckoutStart.aspx'

status=$(get /Checkout/CheckoutStart checkout-start)
check 'GET /Checkout/CheckoutStart signed in -> 302' "$status" 302
# The tutorial ships placeholder PayPal API credentials, so SetExpressCheckout is
# rejected and the page takes its own error branch. That is the boundary: the order
# write beyond it needs credentials this fixture does not have.
case "$(location "$work/checkout-start.head")" in
  */Checkout/CheckoutError.aspx\?ErrorCode=*)
    pass 'CheckoutStart reaches the PayPal boundary and redirects to CheckoutError' ;;
  *)
    fail 'CheckoutStart reaches the PayPal boundary and redirects to CheckoutError' \
      "actual:   $(location "$work/checkout-start.head")" ;;
esac

status=$(get "/Checkout/CheckoutError?ErrorCode=10002&Desc=Security+error" checkout-error)
check 'GET /Checkout/CheckoutError -> 200' "$status" 200
check_contains 'the checkout error page renders the PayPal error code' \
  "$work/checkout-error.html" '10002'

# --- the seeded canEdit user reaches the role-gated admin page -------------------

status=$(get /Account/Login admin-login)
check 'GET /Account/Login for the seeded admin -> 200' "$status" 200
read_postback_fields "$work/admin-login.html"
status=$(post "$(post_url "$work/admin-login.html" /Account/Login)" admin-login-post \
  --data-urlencode 'ctl00$MainContent$Email=canEditUser@wingtiptoys.com' \
  --data-urlencode 'ctl00$MainContent$Password=Pa$$word1' \
  --data-urlencode "$(submit_name "$work/admin-login.html")=Log in")
check 'POST /Account/Login as the Application_Start seeded user -> 302' "$status" 302

status=$(get / home-admin)
check_contains 'IsInRole("canEdit") reveals the Admin link' "$work/home-admin.html" \
  'href="Admin/AdminPage"'

status=$(get /Admin/AdminPage admin)
check 'GET /Admin/AdminPage as canEdit -> 200 (role authorization from claims)' \
  "$status" 200
check_contains 'the admin page binds its product DropDownList' "$work/admin.html" \
  'Convertible Car'

# --- static assets and the production bundles -----------------------------------

status=$(get /Catalog/Images/carconvert.png image)
check 'GET /Catalog/Images/carconvert.png -> 200 (static asset)' "$status" 200

status=$(get /fonts/glyphicons-halflings-regular.woff font)
check 'GET a bootstrap web font -> 200' "$status" 200

check_contains 'the master page renders the Bundle.config style bundle' \
  "$work/home-anon.html" 'href="/Content/css?v='
css=$(grep -o 'href="/Content/css?v=[^"]*"' "$work/home-anon.html" | head -1 |
  sed 's/href="//; s/"$//')
status=$(get "$css" css)
check 'GET the ~/Content/css bundle -> 200' "$status" 200
check_contains 'the style bundle carries bootstrap, minified' "$work/css.html" \
  'html{font-family:sans-serif'
check_contains 'the style bundle carries Site.css too' "$work/css.html" '.body-content'

bundle=$(grep -o 'src="/bundles/WebFormsJs?v=[^"]*"' "$work/home-anon.html" | head -1 |
  sed 's/src="//; s/"$//')
status=$(get "$bundle" webforms)
check 'GET the WebFormsJs bundle -> 200' "$status" 200
check_contains 'the WebFormsJs bundle carries WebForms.js' "$work/webforms.html" '__doPostBack'

echo
if [ "$failures" -eq 0 ]; then
  echo "smoke: all checks passed"
else
  echo "smoke: $failures check(s) failed"
fi
exit $((failures > 0))
