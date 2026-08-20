
const options = {
  method: 'GET',
  headers: {
    accept: 'application/json',
    authorization: 'Bearer 52a773a7-189f-7a69-2d75-68c24af1f715'
  }
};



fetch('https://apisandbox.dev.clover.com/v3/merchants/PDW4DZ6RNQPA1/orders', options)
  .then(response => response.json())
  .then(data => console.log(data))
  .catch(error => console.error(error));